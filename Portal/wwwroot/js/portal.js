/* globals feather:false */

(function () {
    'use strict'
    if(feather){
        feather.replace();
    }
})()

document.addEventListener("DOMContentLoaded", function(event) {
    $('.iiifpreview').tooltip({
        html: true
    });
});

