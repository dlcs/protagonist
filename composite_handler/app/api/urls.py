from app.api.views import CollectionAPIView, QueryMemberAPIView, QueryCollectionAPIView
from django.urls import path

urlpatterns = [
    path("collections/<str:collection_id>", QueryCollectionAPIView.as_view()),
    path(
        "collections/<str:collection_id>/members/<str:member_id>",
        QueryMemberAPIView.as_view(),
    ),
    path("customers/<int:customer>/queue", CollectionAPIView.as_view()),
]
