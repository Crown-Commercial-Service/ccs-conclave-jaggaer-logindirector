class Inputs {
    constructor () {
        this.enforceFormCompletion()
    }

    enforceFormCompletion = () => {
        document.addEventListener("DOMContentLoaded", function (event) {
            // Make sure the form has been completed before allowing the user to proceed
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
    }
}

const initInputs = () => {
    new Inputs()
}

export { initInputs }