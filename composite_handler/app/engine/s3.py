import os
from concurrent.futures import ThreadPoolExecutor, as_completed

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
        successful_uploads = []
        failed_uploads = []

        with tqdm.tqdm(
            desc=f"[{submission_id}] Upload images to S3",
            unit=" image",
            total=len(images),
        ) as progress_bar:
            with ThreadPoolExecutor(max_workers=self._upload_threads) as executor:
                futures = [
                    executor.submit(self.__put_image, submission_id, image)
                    for image in images
                ]
                for future in as_completed(futures):
                    if future.exception():
                        failed_uploads.append(str(future.exception()))
                    else:
                        successful_uploads.append(future.result())
                    progress_bar.update(1)

        if len(failed_uploads) > 0:
            raise Exception("\n".join(failed_uploads))
        return successful_uploads

    def __put_image(self, submission_id, image):
        object_key = f"{self._object_key_prefix}/{submission_id}/{os.path.basename(image.filename)}"
        with open(image.filename, "rb") as file:
            self._client.put_object(Bucket=self._bucket_name, Key=object_key, Body=file)
        return f"{self._bucket_base_url}/{object_key}"
