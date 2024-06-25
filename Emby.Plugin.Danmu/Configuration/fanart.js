define(['baseView', 'loading', 'emby-input', 'emby-button', 'emby-scroller'], function (BaseView, loading) {
    'use strict';

    function loadPage(page, config) {

        console.log(config)
        page.querySelector('#txtFanartApiKey').value = config.UserApiKey || '';

        loading.hide();
    }

    function onSubmit(e) {

        e.preventDefault();

        loading.show();

        var form = this;

        ApiClient.getNamedConfiguration("fanart").then(function (config) {

            config.UserApiKey = form.querySelector('#txtFanartApiKey').value;

            ApiClient.updateNamedConfiguration("fanart", config).then(Dashboard.processServerConfigurationUpdateResult);
        });

        // Disable default form submission
        return false;
    }

    function View(view, params) {
        BaseView.apply(this, arguments);

        view.querySelector('form').addEventListener('submit', onSubmit);
    }

    Object.assign(View.prototype, BaseView.prototype);

    View.prototype.onResume = function (options) {

        BaseView.prototype.onResume.apply(this, arguments);
        console.log("test")
        loading.show();

        var page = this.view;

        ApiClient.getNamedConfiguration("Danmu").then(function (response) {

            loadPage(page, response);
        });
    };

    return View;

});