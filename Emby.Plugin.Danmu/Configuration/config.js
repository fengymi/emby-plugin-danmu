define(
    ['baseView', 'emby-scroller', 'emby-select', 'emby-input', 'emby-checkbox', 'emby-button'],
    function (BaseView) {
        'use strict';

        function View() {
            BaseView.apply(this, arguments);

            var TemplateConfig = {
                pluginUniqueId: 'cdbc5624-3ea9-4f9d-94cc-3be20585f926'
            };
            var container = document.querySelector('#TemplateConfigPage');

            function setButtons() {
                // 设置所有按钮的可见性为 'visible'
                container.querySelectorAll('.sortItem button').forEach(function (button) {
                    button.style.visibility = 'visible';
                });

                // 设置第一项的上移按钮 (btnViewItemUp) 的可见性为 'hidden'
                var firstItemUpButton = container.querySelector('.sortItem:first-child button.btnViewItemUp');
                if (firstItemUpButton) {
                    firstItemUpButton.style.visibility = 'hidden';
                }

                // 设置最后一项的下移按钮 (btnViewItemDown) 的可见性为 'hidden'
                var lastItemDownButton = container.querySelector('.sortItem:last-child button.btnViewItemDown');
                if (lastItemDownButton) {
                    lastItemDownButton.style.visibility = 'hidden';
                }

                // 给所有 sortItem 添加 listItem-border 类
                container.querySelectorAll('.sortItem').forEach(function (sortItem) {
                    sortItem.classList.add('listItem-border');
                });

                var sortItems = container.querySelectorAll('.sortItem');
                sortItems.forEach(function (sortItem, index) {
                    sortItem.setAttribute('data-sort', index);
                });
            }

            function loadConfiguration() {
                Dashboard.showLoadingMsg();
                return ApiClient.getPluginConfiguration(TemplateConfig.pluginUniqueId).then(function (config) {
                    container.querySelector('#current_version').textContent = "v" + config.Version;

                    container.querySelector('#ToAss').checked = config.ToAss;
                    container.querySelector('#AssFont').value = config.AssFont;
                    container.querySelector('#AssFontSize').value = config.AssFontSize;
                    container.querySelector('#AssTextOpacity').value = config.AssTextOpacity;
                    container.querySelector('#AssLineCount').value = config.AssLineCount;
                    container.querySelector('#AssSpeed').value = config.AssSpeed;

                    container.querySelector('#WithRelatedDanmu').checked = config.Dandan.WithRelatedDanmu;
                    container.querySelector('#ChConvert').value = config.Dandan.ChConvert;

                    var scrapersElement = container.querySelector('#Scrapers');
                    scrapersElement.innerHTML = ''; // 清空旧内容

                    config.Scrapers.forEach(function (e, index) {
                        var item = document.createElement('div');
                        item.className = 'listItem listItem-hoverable drop-target ordered-drop-target-y';
                        item.setAttribute('data-action', 'none');
                        item.setAttribute('data-index', index);
                        item.setAttribute('tabindex', '0');
                        item.setAttribute('draggable', 'true');
                        item.dataset.name = e.Name; // 用于标识唯一 scraper 名称

                        item.innerHTML = `
                            <div class="listItem-content listItem-content-margin listItem-content-bg listItemContent-touchzoom listItem-border listItem-border-offset-square">
                                <label data-action="toggleitemchecked"
                                       class="itemAction listItem-emby-checkbox-label emby-checkbox-label secondaryText">
                                    <input tabindex="-1" name="ScraperItem" class="chkItemCheckbox emby-checkbox emby-checkbox-notext" is="emby-checkbox" type="checkbox" ${e.Enable ? "checked" : ""} value="${e.Name}" />
                                    <span class="checkboxLabel listItem-checkboxLabel"></span>
                                </label>
                                <div class="listItemBody itemAction listItemBody-noleftpadding listItemBody-draghandle listItemBody-reduceypadding listItemBody-1-lines">
                                    <div class="listItemBodyText listItemBodyText-lf">${e.Name}</div>
                                </div>
                                <i class="listViewDragHandle dragHandle md-icon listItemIcon listItemIcon-transparent"></i>
                           </div>
                        `;

                        // 拖拽开始事件
                        item.ondragstart = function (event) {
                            event.dataTransfer.setData("text/plain", item.dataset.name); // 使用名称作为标识符
                            item.classList.add('dragging');
                        };

                        scrapersElement.appendChild(item);
                    });

                    // 设置拖拽目标区域（容器）
                    scrapersElement.ondragover = function (event) {
                        event.preventDefault(); // 必须阻止默认行为才能触发 drop
                    };

                    scrapersElement.ondrop = function (event) {
                        event.preventDefault();
                        const draggedName = event.dataTransfer.getData("text/plain");
                        const draggedItem = scrapersElement.querySelector(`[data-name="${draggedName}"]`);

                        if (!draggedItem) return;

                        // 获取插入位置
                        const mouseY = event.clientY;
                        const children = Array.from(scrapersElement.children);
                        let targetIndex = children.findIndex(child => {
                            const rect = child.getBoundingClientRect();
                            return mouseY < rect.top + rect.height / 2;
                        });

                        if (targetIndex === -1) targetIndex = children.length;

                        scrapersElement.insertBefore(draggedItem, children[targetIndex]);
                    };

                    Dashboard.hideLoadingMsg();
                });
            }

            function wrapLoading(promise) {
                Dashboard.showLoadingMsg();
                promise.then(Dashboard.hideLoadingMsg, Dashboard.hideLoadingMsg);
            }

            function onLoad() {
                loadConfiguration();
            }

            container.addEventListener('viewshow', onLoad);

            container.querySelector('#TemplateConfigForm')
                .addEventListener('submit', function (e) {
                    Dashboard.showLoadingMsg();
                    ApiClient.getPluginConfiguration(TemplateConfig.pluginUniqueId).then(function (config) {
                        config.ToAss = document.querySelector('#ToAss').checked;
                        config.AssFont = document.querySelector('#AssFont').value;
                        config.AssFontSize = document.querySelector('#AssFontSize').value;
                        config.AssTextOpacity = document.querySelector('#AssTextOpacity').value;
                        config.AssLineCount = document.querySelector('#AssLineCount').value;
                        config.AssSpeed = document.querySelector('#AssSpeed').value;

                        // 获取当前排序后的 scraper 列表
                        var scrapers = [];
                        let uniqScrapers = new Set();

                        container.querySelectorAll('#Scrapers > .listItem')?.forEach(function (item) {
                            const inputElem = item.querySelector('input[name="ScraperItem"]');
                            if (!inputElem || uniqScrapers.has(inputElem.value)) return;
                            uniqScrapers.add(inputElem.value);

                            var scraper = {
                                Name: inputElem.value,
                                Enable: inputElem.checked
                            };
                            scrapers.push(scraper);
                        });

                        config.Scrapers = scrapers;

                        var dandan = {};
                        dandan.WithRelatedDanmu = container.querySelector('#WithRelatedDanmu').checked;
                        dandan.ChConvert = container.querySelector('#ChConvert').value;
                        config.Dandan = dandan;

                        ApiClient.updatePluginConfiguration(TemplateConfig.pluginUniqueId, config).then(function (result) {
                            Dashboard.processPluginConfigurationUpdateResult(result);
                        });
                    });

                    e.preventDefault();
                    return false;
                });
        }

        Object.assign(View.prototype, BaseView.prototype);
        View.prototype.onResume = function (options) {
            BaseView.prototype.onResume.apply(this, arguments);
        };

        return View;
    }
);