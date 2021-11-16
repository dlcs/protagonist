import logging

logger = logging.getLogger(__name__)


def print_result(task):
    logger.info(
        "Task [" + task.name + "] completed with result [" + str(task.result) + "]"
    )
