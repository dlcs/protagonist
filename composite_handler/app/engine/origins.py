import os
from pathlib import Path

import requests
from django.conf import settings


class HttpOrigin:
    def __init__(self):
        self._scratch_path = settings.SCRATCH_DIRECTORY
        self._chunk_size = settings.ORIGIN_CONFIG["chunk_size"]

    def fetch(self, submission_id, url, file_extension="pdf"):
        subfolder_path = self.__generate_subfolder_path(submission_id)
        file_path = os.path.join(subfolder_path, "source." + file_extension)
        with requests.get(url, stream=True) as response:
            response.raise_for_status()
            with open(file_path, "wb") as file:
                for chunk in response.iter_content(chunk_size=self._chunk_size):
                    file.write(chunk)
        return subfolder_path

    def __generate_subfolder_path(self, submission_id):
        subfolder_path = self._scratch_path / Path(str(submission_id))
        Path(subfolder_path).mkdir(parents=True, exist_ok=True)
        return subfolder_path
