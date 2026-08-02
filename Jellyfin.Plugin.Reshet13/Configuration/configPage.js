const pluginId = '13c0a101-6f42-4a8e-9d21-5b7c8e4f2a63';

export default function (view) {
    function loadConfiguration() {
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            view.querySelector('#IsEnabled').checked = config.IsEnabled !== false;
            view.querySelector('#Catalogs').value = config.Catalogs || '';

            view.querySelector('#MaximumSeries').value = config.MaximumSeries || 1000;
            view.querySelector('#CacheMinutes').value = config.CacheMinutes || 360;
            view.querySelector('#UserAgent').value = config.UserAgent || '';
            Dashboard.hideLoadingMsg();
        });
    }

    view.querySelector('#Reshet13ConfigForm').addEventListener('submit', function (event) {
        event.preventDefault();
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.IsEnabled = view.querySelector('#IsEnabled').checked;
            config.Catalogs = view.querySelector('#Catalogs').value.trim();

            config.MaximumSeries = Number.parseInt(
                view.querySelector('#MaximumSeries').value,
                10);
            config.CacheMinutes = Number.parseInt(
                view.querySelector('#CacheMinutes').value,
                10);
            config.UserAgent = view.querySelector('#UserAgent').value.trim();

            ApiClient.updatePluginConfiguration(pluginId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });

        return false;
    });

    view.addEventListener('viewshow', loadConfiguration);
}
