#! /bin/bash

# create buckets
awslocal s3 mb s3://dlcs-output
awslocal s3 mb s3://dlcs-thumbs
awslocal s3 mb s3://dlcs-storage
awslocal s3 mb s3://dlcs-timebased-in

# create queues
awslocal sqs create-queue --queue-name dlcs-image
awslocal sqs create-queue --queue-name dlcs-priority-image
awslocal sqs create-queue --queue-name dlcs-timebased
awslocal sqs create-queue --queue-name dlcs-file
awslocal sqs create-queue --queue-name dlcs-transcode-complete