from app.api.views import CollectionAPIView, QueryAPIView
from django.urls import path

urlpatterns = [
    path("collections/<str:id>", QueryAPIView.as_view()),
    path("customers/<int:customer>/queue", CollectionAPIView.as_view()),
]
