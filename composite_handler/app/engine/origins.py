import os
import uuid
from pathlib import Path

import requests
from app.settings import SCRATCH_DIRECTORY, ORIGIN_CHUNK_SIZE


class HttpOrigin:

    def __init__(self):
        self._scratch_path = SCRATCH_DIRECTORY
        self._chunk_size = ORIGIN_CHUNK_SIZE

    def fetch(self, submission_id, url, file_extension="pdf"):
        subfolder_path = self.__generate_subfolder_path(submission_id)
        file_path = os.path.join(subfolder_path, "source." + file_extension)
        with requests.get(url, stream=True) as response:
            response.raise_for_status()
            with open(file_path, 'wb') as file:
                for chunk in response.iter_content(chunk_size=self._chunk_size):
                    file.write(chunk)
        return subfolder_path

    def __generate_subfolder_path(self, submission_id):
        subfolder_path = os.path.join(self._scratch_path, str(submission_id))
        Path(subfolder_path).mkdir(parents=True, exist_ok=True)
        return subfolder_path
