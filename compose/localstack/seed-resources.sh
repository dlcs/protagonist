#! /bin/bash

# create buckets
awslocal s3 mb s3://dlcs-output
awslocal s3 mb s3://dlcs-thumbs
awslocal s3 mb s3://dlcs-storage
awslocal s3 mb s3://dlcs-origin
awslocal s3 mb s3://dlcs-timebased-in
awslocal s3 mb s3://dlcs-timebased-out

# create queues
awslocal sqs create-queue --queue-name dlcs-image
awslocal sqs create-queue --queue-name dlcs-priority-image
awslocal sqs create-queue --queue-name dlcs-timebased
awslocal sqs create-queue --queue-name dlcs-file
awslocal sqs create-queue --queue-name dlcs-transcode-complete
awslocal sqs create-queue --queue-name dlcsspinup-delete-notification

# create topics
awslocal sns create-topic --name asset-modified-notifications

# check attributes
awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/dlcsspinup-delete-notification/ --attribute-names QueueArn

# subscribe SQS to SNS
SUBSCRIPTION=`awslocal sns subscribe --topic-arn arn:aws:sns:us-east-1:000000000000:asset-modified-notifications --protocol sqs --notification-endpoint arn:aws:sqs:us-east-1:000000000000:dlcsspinup-delete-notification --output text  | awk -F\"SubscriptionArn\": '{print $NF}'`

# set filter policy on subscription
awslocal sns set-subscription-attributes --subscription-arn $SUBSCRIPTION --attribute-name FilterPolicy --attribute-value '{"messageType":["Delete"]}'

# log subscription
awslocal sns get-subscription-attributes --subscription-arn $SUBSCRIPTION