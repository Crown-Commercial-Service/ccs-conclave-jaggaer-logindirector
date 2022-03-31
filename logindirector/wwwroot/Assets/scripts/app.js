document.addEventListener("DOMContentLoaded", function (event) {
    // Custom functions
    $('#account-create').change(function (e) {
        $('#merge-banner').removeClass('banner-hidden');
    });

    $('#account-merge').change(function (e) {
        $('#merge-banner').addClass('banner-hidden');
    });
});
