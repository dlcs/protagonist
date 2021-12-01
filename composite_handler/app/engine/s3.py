import os
from concurrent.futures import ThreadPoolExecutor
from itertools import repeat

import boto3
import tqdm
from django.conf import settings


class S3Client:
    def __init__(self):
        self._client = boto3.session.Session().client("s3")
        self._bucket_name = settings.DLCS["s3_bucket_name"]
        self._bucket_base_url = self.__build_bucket_base_url()
        self._object_key_prefix = settings.DLCS["s3_object_key_prefix"].strip("/")
        self._upload_threads = settings.DLCS["s3_upload_threads"]

    def __build_bucket_base_url(self):
        # Bucket located in 'us-east-1' don't have a location constraint in the hostname
        location = self._client.get_bucket_location(Bucket=self._bucket_name)[
            "LocationConstraint"
        ]

        if location:
            return f"https://s3-{location}.amazonaws.com/{self._bucket_name}"
        else:
            return f"https://s3.amazonaws.com/{self._bucket_name}"

    def put_images(self, submission_id, images):
        s3_uris = []

        with tqdm.tqdm(
            desc=f"[{submission_id}] Upload images to S3",
            unit=" image",
            total=len(images),
        ) as progress_bar:
            with ThreadPoolExecutor(max_workers=self._upload_threads) as executor:
                # It's critical that the list of S3 URI's returned by this method is in the
                # same order as the list of images provided to it. '.map(...)' gives us that,
                # whilst '.submit(...)' does not.
                for s3_uri in executor.map(
                    self.__put_image, repeat(submission_id), images
                ):
                    s3_uris.append(s3_uri)
                    progress_bar.update(1)
        return s3_uris

    def __put_image(self, submission_id, image):
        object_key = f"{self._object_key_prefix}/{submission_id}/{os.path.basename(image.filename)}"
        with open(image.filename, "rb") as file:
            self._client.put_object(Bucket=self._bucket_name, Key=object_key, Body=file)
        return f"{self._bucket_base_url}/{object_key}"
