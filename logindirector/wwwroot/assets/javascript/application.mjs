import jQuery from 'jquery';
import { initAll } from 'govuk-frontend'
import { initAllComponents } from './components/all.mjs'

window.$ ||= jQuery
window.jQuery ||= jQuery

$('body').addClass(' js-enabled' + ('noModule' in HTMLScriptElement.prototype ? ' govuk-frontend-supported' : ''));

initAll();

$(() => {
    initAllComponents()
})
