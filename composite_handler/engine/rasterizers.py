import os

from app.settings import PDF_RASTERIZER_DPI
from app.settings import PDF_RASTERIZER_FORMAT
from app.settings import PDF_RASTERIZER_THREAD_COUNT
from pdf2image import convert_from_path


class PdfRasterizer:

    def __init__(self):
        self._dpi = PDF_RASTERIZER_DPI
        self._fmt = PDF_RASTERIZER_FORMAT
        self._thread_count = PDF_RASTERIZER_THREAD_COUNT

    def rasterize_pdf(self, subfolder_path):
        # Typically, pdf2image will write generated images to a temporary path, after
        # which you can manipulate them. By providing 'output_file' and 'output_folder',
        # we can skip that second step and make pdf2image write directly to our desired
        # output folder, using our desired file name pattern.
        return convert_from_path(
            os.path.join(subfolder_path, "source.pdf"),
            dpi=self._dpi,
            fmt=self._fmt,
            thread_count=self._thread_count,
            output_file="image-",
            output_folder=subfolder_path
        )
