import logging

from app.api.serializers import CollectionSerializer, MemberSerializer
from app.common.dlcs import DLCS
from app.common.models import Collection, Member
from django.core.exceptions import PermissionDenied
from django_q.tasks import async_task
from rest_framework import status
from rest_framework.response import Response
from rest_framework.views import APIView

logger = logging.Logger(__name__)


class AbstractAPIView(APIView):
    # Disable Django's built-in authentication framework for
    # these views, otherwise Django will attempt to check
    # provided credentials against its own user database.
    authentication_classes = []

    def __init__(self, **kwargs):
        super().__init__(**kwargs)
        self._dlcs = DLCS()

    def _validate_credentials(self, customer, headers):
        if not customer or "Authorization" not in headers:
            raise PermissionDenied
        self._dlcs.test_credentials(customer, headers["Authorization"])


class QueryAPIView(AbstractAPIView):
    def get(self, request, *args, **kwargs):

        try:
            collection = Collection.objects.get(id=kwargs["id"])
        except Collection.DoesNotExist:
            return Response(status=status.HTTP_404_NOT_FOUND)

        self._validate_credentials(collection.customer, request.headers)

        return Response(
            [
                self._build_member_response(member)
                for member in Member.objects.filter(collection=collection)
            ],
            status=status.HTTP_200_OK,
        )

    def _build_member_response(self, member):
        response = {
            "id": member.id,
            "status": member.status,
            "last_updated": member.last_updated_date,
        }

        if member.image_count:
            response["image_count"] = member.image_count
        if member.dlcs_uri:
            response["dlcs_uri"] = member.dlcs_uri
        if member.error:
            response["error"] = member.error

        return response


class CollectionAPIView(AbstractAPIView):
    def post(self, request, *args, **kwargs):

        self._validate_credentials(kwargs["customer"], request.headers)

        serializer = CollectionSerializer(
            data={"json_data": request.data, "customer": kwargs["customer"]}
        )
        if not serializer.is_valid():
            return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
        serializer.save()

        collection_id = serializer.data["id"]

        serializers = [
            MemberSerializer(data={"json_data": member, "collection": collection_id})
            for member in serializer.data["json_data"]["member"]
        ]

        if not all(serializer.is_valid() for serializer in serializers):
            return Response(status=status.HTTP_400_BAD_REQUEST)

        for serializer in serializers:
            serializer.save()
            async_task(
                "app.engine.tasks.process_member",
                {"id": serializer.data["id"], "auth": request.headers["Authorization"]},
                task_name=serializer.data["id"],
                hook="app.engine.hooks.print_result",
            )

        return Response(
            status=status.HTTP_202_ACCEPTED,
            headers={
                "Location": "{0}://{1}/collections/{2}".format(
                    request.scheme, request.get_host(), collection_id
                )
            },
        )
