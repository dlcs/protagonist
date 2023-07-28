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
awslocal sqs create-queue --queue-name dlcs-delete-notifcations

# create topics
awslocal sns create-topic --name dlcs-delete-notification

# subscribe
awslocal sns subscribe --topic-arn   arn:aws:sns:eu-west-1:000000000000:dlcs-delete-notifcations --protocol sqs