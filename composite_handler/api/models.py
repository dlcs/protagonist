import uuid

from django.db import models


class Submission(models.Model):
    id = models.UUIDField(name="id", primary_key=True, default=uuid.uuid4, editable=False)
    payload = models.JSONField()
    status = models.CharField(max_length=10, default="PENDING")
    customer = models.IntegerField()
    space = models.IntegerField(null=True, blank=True)
    image = models.IntegerField(null=True, blank=True)
    dlcs_uri = models.TextField(null=True, blank=True)
    error = models.TextField(null=True, blank=True)
    created_date = models.DateTimeField(auto_now_add=True)
    last_updated_date = models.DateTimeField(auto_now=True)

    def __str__(self):
        return str(self.id)
