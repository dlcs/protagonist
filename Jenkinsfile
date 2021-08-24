pipeline {
 agent {
  node {
   label 'master'
  }
 }
 options {
  buildDiscarder(logRotator(numToKeepStr: '100'))
 }
 stages {
  stage('Fetch') {
   steps {
    deleteDir()
    checkout scm: [
     $class: 'GitSCM',
     branches: scm.branches,
     doGenerateSubmoduleConfigurations: false,
     extensions: [
      [$class: 'SubmoduleOption',
       disableSubmodules: false,
       parentCredentials: true,
       recursiveSubmodules: true,
       reference: '',
       trackingSubmodules: false
      ]
     ],
     submoduleCfg: [],
     userRemoteConfigs: scm.userRemoteConfigs
    ]
   }
  }
  stage('Install') {
    steps {
      sh "apt-get update"
      sh "apt install -y libunwind8 gettext apt-transport-https python3-pip"
      sh "pip3 install awscli"
      sh "curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 5.0"
    }
  }
  stage('Test') {
    steps {
      sh "~/.dotnet/dotnet test protagonist.sln --logger trx --filter 'Category!=Database&Category!=Manual&Category!=Integration'"
      step([$class: 'MSTestPublisher', testResultsFile:"**/*.trx", failOnError: true, keepLongStdio: true])
    }
  }
  stage('Image') {
   steps {
    sh "docker build -t ${DOCKER_IMAGE}:latest -f ${DOCKER_FILE} ."
   }
  }
  stage('Tag') {
   steps {
    sh "docker tag ${DOCKER_IMAGE}:latest ${DOCKER_IMAGE}:`git rev-parse HEAD`"
    sh "docker tag ${DOCKER_IMAGE}:latest ${DOCKER_IMAGE}:production"
   }
  }
  stage('Push') {
   steps {
    sh "\$(aws ecr get-login --no-include-email --region ${REGION})"
    sh "docker push ${DOCKER_IMAGE}:`git rev-parse HEAD`"
    sh "docker push ${DOCKER_IMAGE}:production"
   }
  }
  stage('Bounce') {
   steps {
    sh "curl --data '{\"text\": \"Jenkins bouncing ${REGION}/${CLUSTER}/${SERVICE}...\"}' ${SLACK_WEBHOOK_URL}"
    script{
          for (s in env.SERVICE.split(',')) {
            bounceService(s)
          }      
        }
    sh "curl --data '{\"text\": \"${REGION}/${CLUSTER}/${SERVICE} is now stable\"}' ${SLACK_WEBHOOK_URL}"
   }
  }
 }
}

def bounceService(String service) {
    sh "aws ecs update-service --force-new-deployment --cluster ${CLUSTER} --service ${service} --region ${REGION}"
    sh "aws ecs wait services-stable --cluster ${CLUSTER} --services ${service} --region ${REGION}"
}