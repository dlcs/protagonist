from api.views import SubmissionAPICollectionView, SubmissionAPIMemberView, SubmissionAPIQueryView
from django.urls import path

urlpatterns = [
    path('submissions/<str:id>', SubmissionAPIQueryView.as_view()),
    path('customers/<int:customer>/queue', SubmissionAPICollectionView.as_view()),
    path('customers/<int:customer>/spaces/<int:space>/images/<int:image>', SubmissionAPIMemberView.as_view())
]
