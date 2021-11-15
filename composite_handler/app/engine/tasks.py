from app.common.models import Member
from app.engine.builder import MemberBuilder
from app.engine.origins import HttpOrigin
from app.engine.rasterizers import PdfRasterizer
from app.engine.s3 import S3Client

http_origin = HttpOrigin()
pdf_rasterizer = PdfRasterizer()
s3_client = S3Client()


def process_member(member_id):
    member = Member.objects.get(id=member_id)
    try:
        pdf_path = __fetch_origin(member, member.json_data["origin"])
        images = __rasterize_composite(member, pdf_path)
        s3_urls = __push_images_to_dlcs(member, images)
        dlcs_request = __build_dlcs_request(member, s3_urls)
        dlcs_response = __initiate_dlcs_ingest(member, dlcs_request)
        return __build_result(member, dlcs_response)
    except Exception as error:
        __process_error(member, error)


def __fetch_origin(member, origin_uri):
    __update_status(member, "FETCHING_ORIGIN")
    return http_origin.fetch(member.id, origin_uri)


def __rasterize_composite(member, pdf_path):
    __update_status(member, "RASTERIZING")
    return pdf_rasterizer.rasterize_pdf(pdf_path)


def __push_images_to_dlcs(member, images):
    __update_status(member, "PUSHING_TO_DLCS", image_count=len(images))
    s3_urls = []
    for image in images:
        s3_url = s3_client.put_image(member.id, image.filename)
        s3_urls.append(s3_url)
    return s3_urls


def __build_dlcs_request(member, dlcs_uris):
    member_builder = MemberBuilder(member.json_data)
    for dlcs_uri in dlcs_uris:
        member_builder.build_member(dlcs_uri)
    __update_status(member, "BUILDING_DLCS_REQUEST")
    return member_builder.build_collection()


def __initiate_dlcs_ingest(member, dlcs_request):
    __update_status(member, "SUBMITTING_TO_DLCS")
    return {"@id": "https://dlcs.digirati.io/abc123"}


def __build_result(member, dlcs_response):
    __update_status(member, "COMPLETED", dlcs_uri=dlcs_response["@id"])
    return {"Result": True}


def __process_error(member, error):
    __update_status(member, "ERROR", error=str(error))
    raise error


def __update_status(member, status, image_count=None, dlcs_uri=None, error=None):
    member.status = status
    if image_count:
        member.image_count = image_count
    if dlcs_uri:
        member.dlcs_uri = dlcs_uri
    if error:
        member.error = error
    member.save()
