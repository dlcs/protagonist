from api.models import Submission
from api.serializers import SubmissionCollectionSerializer, SubmissionMemberSerializer
from django_q.tasks import async_task
from rest_framework import status
from rest_framework.response import Response
from rest_framework.views import APIView


class SubmissionAPIQueryView(APIView):
    def get(self, request, *args, **kwargs):
        try:
            submission = Submission.objects.get(id=kwargs["id"])
            if submission.status == "PENDING":
                return Response(status=status.HTTP_200_OK)
            elif submission.status == "COMPLETED":
                return Response(status=status.HTTP_303_SEE_OTHER, headers={"Location": submission.dlcs_uri})
            elif submission.status == "ERROR":
                return Response({"Error": submission.error}, status=status.HTTP_422_UNPROCESSABLE_ENTITY)
        except Submission.DoesNotExist:
            return Response(status=status.HTTP_404_NOT_FOUND)


class SubmissionAPICollectionView(APIView):
    def post(self, request, *args, **kwargs):
        data = {
            "payload": request.data,
            "customer": kwargs["customer"],
        }
        serializer = SubmissionCollectionSerializer(data=data)
        return _build_submissions_creation_response(request, serializer)


class SubmissionAPIMemberView(APIView):
    def post(self, request, *args, **kwargs):
        data = {
            "payload": request.data,
            "customer": kwargs["customer"],
            "space": kwargs["space"],
            "image": kwargs["image"]
        }
        serializer = SubmissionMemberSerializer(data=data)
        return _build_submissions_creation_response(request, serializer)


def _build_submissions_creation_response(request, serializer):
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    serializer.save()
    async_task(
        "engine.tasks.process_submission",
        serializer.data,
        task_name=serializer.data["id"],
        hook='engine.hooks.print_result'
    )
    return Response(
        status=status.HTTP_202_ACCEPTED,
        headers={"Location": _build_uri(request, serializer.data["id"])}
    )


def _build_uri(request, path):
    return request.scheme + "://" + request.get_host() + "/submissions/" + path
