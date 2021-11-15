import os

import boto3
from app.settings import TARGET_S3_BUCKET_NAME


class S3Client:
    def __init__(self):
        self._client = boto3.client("s3")
        self._bucket_name = TARGET_S3_BUCKET_NAME
        self._bucket_base_url = "https://s3-{0}.amazonaws.com/{1}".format(
            self._client.get_bucket_location(Bucket=self._bucket_name)[
                "LocationConstraint"
            ],
            self._bucket_name,
        )

    def put_image(self, submission_id, image_path):
        object_key = str(submission_id) + "/" + os.path.basename(image_path)
        with open(image_path, "rb") as file:
            self._client.put_object(Bucket=self._bucket_name, Key=object_key, Body=file)
        return "{0}/{1}".format(self._bucket_base_url, object_key)
