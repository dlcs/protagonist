from django.conf import settings


class MemberBuilder:
    STATIC_FIELDS = {"mediaType": "image/jpeg", "family": "I"}

    STRIP_FIELDS = ["@type", "originFormat", "incrementSeed"]

    FORMAT_FIELDS = [
        "id",
        "string1",
        "string2",
        "string3",
        "number1",
        "number2",
        "number3",
        "text",
    ]

    def __init__(self, template):
        self._index = template["incrementSeed"]
        self._template = self.__build_template(template)
        self.batch_size = settings.DLCS["batch_size"]
        self._members = []

    def __build_template(self, original_template):
        template = dict(original_template) | self.STATIC_FIELDS
        for strip_field in self.STRIP_FIELDS:
            template.pop(strip_field)
        return template

    def build_member(self, dlcs_uri):
        template = dict(self._template)
        for format_field in self.FORMAT_FIELDS:
            if format_field in template and isinstance(template[format_field], str):
                template[format_field] = template[format_field].format(self._index)
        template["origin"] = dlcs_uri
        self._index += 1
        self._members.append(template)

    def build_collections(self):
        collections = []
        for chunked_member in self._chunk_members(self._members, self.batch_size):
            collections.append(
                {
                    "@context": "http://www.w3.org/ns/hydra/context.jsonld",
                    "@type": "Collection",
                    "member": chunked_member,
                }
            )
        return collections

    def _chunk_members(self, members, size):
        for i in range(0, len(members), size):
            yield members[i : i + size]
