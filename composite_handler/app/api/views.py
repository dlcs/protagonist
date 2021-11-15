import logging

from app.api.serializers import CollectionSerializer, MemberSerializer
from app.common.models import Collection, Member
from django_q.tasks import async_task
from rest_framework import status
from rest_framework.response import Response
from rest_framework.views import APIView

logger = logging.Logger(__name__)


class QueryAPIView(APIView):
    def get(self, request, *args, **kwargs):
        try:
            collection = Collection.objects.get(id=kwargs["id"])
        except Collection.DoesNotExist:
            return Response(status=status.HTTP_404_NOT_FOUND)

        response = []
        for member in Member.objects.filter(collection=collection):
            response.append({
                "id": member.id,
                "status": member.status,
                "last_updated": member.last_updated_date,
                **({"image_count": member.image_count} if member.image_count else {}),
                **({"dlcs_uri": member.dlcs_uri} if member.dlcs_uri else {}),
                **({"error": member.error} if member.error else {})
            })
        return Response(response, status=status.HTTP_200_OK)


class CollectionAPIView(APIView):
    def post(self, request, *args, **kwargs):

        serializer = CollectionSerializer(data={"json_data": request.data, "customer": kwargs["customer"]})
        if not serializer.is_valid():
            return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
        serializer.save()

        collection_id = serializer.data["id"]

        for member in serializer.data["json_data"]["member"]:
            serializer = MemberSerializer(data={"json_data": member, "collection": collection_id})
            if serializer.is_valid():
                serializer.save()

                async_task(
                    "app.engine.tasks.process_member",
                    serializer.data["id"],
                    task_name=serializer.data["id"],
                    hook='app.engine.hooks.print_result'
                )
            else:
                logger.error("Failed to save member [{0}]: {1}".format(member, serializer.errors))

        return Response(
            status=status.HTTP_202_ACCEPTED,
            headers={
                "Location": "{0}://{1}/collections/{2}".format(request.scheme, request.get_host(), collection_id)
            }
        )
