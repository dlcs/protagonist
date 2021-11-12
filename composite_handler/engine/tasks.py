from engine.origins import HttpOrigin
from engine.rasterizers import PdfRasterizer

http_origin = HttpOrigin()
pdf_rasterizer = PdfRasterizer()


def process_submission(submission):
    json_data = submission["payload"]
    scratch_file_path = http_origin.fetch(json_data["origin"])
    images = pdf_rasterizer.rasterize_pdf(scratch_file_path)
    return "Processed [" + str(len(images)) + "] images for PDF [" + json_data["origin"] + "]"
