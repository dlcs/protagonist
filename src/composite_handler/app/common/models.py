import uuid

from django.db import models


class Collection(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    json_data = models.JSONField()
    customer = models.IntegerField()
    created_date = models.DateTimeField(auto_now_add=True)
    last_updated_date = models.DateTimeField(auto_now=True)

    def __str__(self):
        return str(self.id)


class Member(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    collection = models.ForeignKey(Collection, on_delete=models.CASCADE)
    json_data = models.JSONField()
    status = models.CharField(max_length=21, default="PENDING")
    image_count = models.IntegerField(null=True, blank=True)
    error = models.TextField(null=True, blank=True)
    created_date = models.DateTimeField(auto_now_add=True)
    last_updated_date = models.DateTimeField(auto_now=True)

    def __str__(self):
        return str(self.id)


class DLCSBatch(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    member = models.ForeignKey(Member, on_delete=models.CASCADE)
    json_data = models.JSONField()
    uri = models.TextField(null=True, blank=False)
    created_date = models.DateTimeField(auto_now_add=True)
    last_updated_date = models.DateTimeField(auto_now=True)

    def __str__(self):
        return str(self.id)
