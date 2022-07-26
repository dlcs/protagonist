import logging

from app.api.serializers import CollectionSerializer, MemberSerializer
from app.common.dlcs import DLCS
from app.common.models import Collection, Member, DLCSBatch
from django.conf import settings
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
        self._scheme = settings.WEB_SERVER["scheme"]
        self._hostname = settings.WEB_SERVER["hostname"]
        self._dlcs = DLCS()

    def _validate_credentials(self, customer, headers):
        if not customer or "Authorization" not in headers:
            raise PermissionDenied
        self._dlcs.test_credentials(customer, headers["Authorization"])

    def _build_collection_response_body(self, collection):
        return {
            "id": f"{self._scheme}://{self._hostname}/collections/{collection.id}",
            "members": [
                self._build_member_response_body(member)
                for member in Member.objects.filter(collection=collection)
            ],
        }

    def _build_member_response_body(self, member):
        response = {
            "id": f"{self._scheme}://{self._hostname}/collections/{member.collection}/members/{member.id}",
            "status": member.status,
            "created": member.created_date,
            "last_updated": member.last_updated_date,
        }

        if member.image_count:
            response["image_count"] = member.image_count
        if member.error:
            response["error"] = member.error

        dlcs_batches = DLCSBatch.objects.filter(member=member.id)
        if len(dlcs_batches) > 0:
            response["dlcs_uris"] = [dlcs_batch.uri for dlcs_batch in dlcs_batches]

        return response


class QueryCollectionAPIView(AbstractAPIView):
    def get(self, request, *args, **kwargs):

        try:
            collection = Collection.objects.get(id=kwargs["collection_id"])
        except Collection.DoesNotExist:
            return Response(status=status.HTTP_404_NOT_FOUND)

        self._validate_credentials(collection.customer, request.headers)

        return Response(
            self._build_collection_response_body(collection),
            status=status.HTTP_200_OK,
        )


class QueryMemberAPIView(AbstractAPIView):
    def get(self, request, *args, **kwargs):
        try:
            member = Member.objects.get(
                id=kwargs["member_id"], collection=kwargs["collection_id"]
            )
        except Member.DoesNotExist:
            return Response(status=status.HTTP_404_NOT_FOUND)

        self._validate_credentials(member.collection.customer, request.headers)

        response_body = self._build_member_response_body(member)
        response_status = (
            status.HTTP_422_UNPROCESSABLE_ENTITY
            if member.status == "ERROR"
            else status.HTTP_200_OK
        )

        return Response(response_body, response_status)


class CollectionAPIView(AbstractAPIView):
    def post(self, request, *args, **kwargs):

        self._validate_credentials(kwargs["customer"], request.headers)

        serializer = CollectionSerializer(
            data={"json_data": request.data, "customer": kwargs["customer"]}
        )
        if not serializer.is_valid():
            return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
        collection = serializer.save()

        serializers = [
            MemberSerializer(data={"json_data": member, "collection": collection.id})
            for member in collection.json_data["member"]
        ]

        if not all(serializer.is_valid() for serializer in serializers):
            return Response(status=status.HTTP_400_BAD_REQUEST)

        for serializer in serializers:
            member = serializer.save()
            async_task(
                "app.engine.tasks.process_member",
                {"id": member.id, "auth": request.headers["Authorization"]},
                task_name=f"Submission: [{member.id}]",
            )

        return Response(
            self._build_collection_response_body(collection),
            status=status.HTTP_202_ACCEPTED,
        )
