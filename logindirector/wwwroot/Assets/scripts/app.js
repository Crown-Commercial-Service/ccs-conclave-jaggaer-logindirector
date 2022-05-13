document.addEventListener("DOMContentLoaded", function (event) {
    // Custom functions
    $('#merge-selection-form').submit(function (e) {
        if ($("input[name='accountDecision']:checked").val()) {
            // Value has been selected, proceed
            return true;
        } else {
            // Value not selected - trigger error display
            $('#merge-selection-group').addClass('govuk-form-group--error');
            $('#merge-prompt-error').removeClass('govuk-visually-hidden');
            return false;
        }
    });
});
