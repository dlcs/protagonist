from app.common.models import DLCSBatch
from rest_framework import serializers


class DLCSBatchSerializer(serializers.ModelSerializer):
    class Meta:
        model = DLCSBatch
        fields = "__all__"
