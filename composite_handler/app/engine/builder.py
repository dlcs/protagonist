class MemberBuilder:
    STATIC_FIELDS = {"mediaType": "image/jp2"}

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
        self._members = []

    def __build_template(self, original_template):
        template = dict(original_template) | self.STATIC_FIELDS
        for strip_field in self.STRIP_FIELDS:
            template.pop(strip_field)
        return template

    def build_member(self, dlcs_uri):
        template = dict(self._template)
        for format_field in self.FORMAT_FIELDS:
            if format_field in template:
                template[format_field] = template[format_field].format(self._index)
        template["origin"] = dlcs_uri
        self._index += 1
        self._members.append(template)

    def build_collection(self):
        return {
            "@context": "http://www.w3.org/ns/hydra/context.jsonld",
            "@type": "Collection",
            "member": self._members,
        }
