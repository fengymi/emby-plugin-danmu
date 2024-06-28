define(
    ['baseView', 'emby-scroller', 'emby-select', 'emby-input', 'emby-checkbox', 'emby-button'],
    function (BaseView) {
        'use strict';

        function View() {
            // BaseView.apply(this, arguments);

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

            function onLoad() {
                Dashboard.showLoadingMsg();
                ApiClient.getPluginConfiguration(TemplateConfig.pluginUniqueId).then(function (config) {
                    container.querySelector('#current_version').textContent = "v" + config.Version;

                    container.querySelector('#ToAss').checked = config.ToAss;
                    container.querySelector('#OpenAllSource').checked = config.OpenAllSource;
                    container.querySelector('#AssFont').value = config.AssFont;
                    container.querySelector('#AssFontSize').value = config.AssFontSize;
                    container.querySelector('#AssTextOpacity').value = config.AssTextOpacity;
                    container.querySelector('#AssLineCount').value = config.AssLineCount;
                    container.querySelector('#AssSpeed').value = config.AssSpeed;

                    container.querySelector('#WithRelatedDanmu').checked = config.Dandan.WithRelatedDanmu;
                    container.querySelector('#ChConvert').value = config.Dandan.ChConvert;


                    var html = '';
                    config.Scrapers.forEach(function (e) {
                        html += '<div class="listItem listItem-border sortableOption sortItem" data-sort="' + e + '">';
                        html += '    <label class="listItemCheckboxContainer emby-checkbox-label">';
                        html += '        <input type="checkbox" is="emby-checkbox" class="chkEnableCodec emby-checkbox emby-checkbox-focusring" name="ScraperItem" ' + (e.Enable ? 'checked' : '') + ' value="' + e.Name + '" >';
                        html += '        <span class="checkboxLabel" style="width:200px" >' + e.Name + '</span>';
                        html += '    </label>';
                        html += '    <div class="listItemBody two-line listItemBodyText"></div>';
                        html += '    <button type="button" is="paper-icon-button-light" title="上" class="btnSortable paper-icon-button-light btnSortableMoveUp btnViewItemUp" data-pluginindex="2"><span class="material-icons keyboard_arrow_up"></span></button>';
                        html += '    <button type="button" is="paper-icon-button-light" title="下" class="btnSortable paper-icon-button-light btnSortableMoveDown btnViewItemDown" data-pluginindex="0"><span class="material-icons keyboard_arrow_down"></span></button>';
                        html += '</div>';
                        html += '\r\n';
                    });

                    // container.querySelector('#Scrapers').empty().append(html);
                    var scrapersElement = container.querySelector('#Scrapers');
                    // 清空元素内容
                    while (scrapersElement.firstChild) {
                        scrapersElement.removeChild(scrapersElement.firstChild);
                    }
                    // 创建一个新的元素来承载 HTML 内容，如果 html 是一个字符串
                    var div = document.createElement('div');
                    div.innerHTML = html;
                    // 现在，将创建的元素内的子节点逐个追加到目标元素
                    while (div.firstChild) {
                        scrapersElement.appendChild(div.firstChild);
                    }

                    setButtons();
                    Dashboard.hideLoadingMsg();
                });
            }

            container.addEventListener('viewshow', onLoad);

            container.querySelector('#TemplateConfigForm')
                .addEventListener('submit', function (e) {
                    Dashboard.showLoadingMsg();
                    ApiClient.getPluginConfiguration(TemplateConfig.pluginUniqueId).then(function (config) {
                        config.ToAss = document.querySelector('#ToAss').checked;
                        config.OpenAllSource = document.querySelector('#OpenAllSource').checked;
                        config.AssFont = document.querySelector('#AssFont').value;
                        config.AssFontSize = document.querySelector('#AssFontSize').value;
                        config.AssTextOpacity = document.querySelector('#AssTextOpacity').value;
                        config.AssLineCount = document.querySelector('#AssLineCount').value;
                        config.AssSpeed = document.querySelector('#AssSpeed').value;

                        var scrapers = [];
                        document.querySelectorAll('input[name="ScraperItem"]').forEach(function (inputElem) {
                            var scraper = new Object();
                            scraper.Name = inputElem.value;
                            scraper.Enable = inputElem.checked;
                            scrapers.push(scraper);
                        });
                        config.Scrapers = scrapers;

                        var dandan = new Object();
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

            // container.addEventListener('DOMContentLoaded', function () {
            //     setButtons();
            //     // container.querySelectorAll('.btnViewItemDown').forEach()
            //     container.addEventListener('click', function (e) {
            //         // 检查是否点击了.btnViewItemDown按钮
            //         if (e.target && e.target.matches('.btnViewItemDown')) {
            //             var cCard = e.target.closest('.sortItem');
            //             var tCard = cCard.nextElementSibling;
            //             if (tCard && tCard.matches('.sortItem')) {
            //                 cCard.parentNode.insertBefore(tCard, cCard);
            //                 setButtons();
            //             }
            //         }
            //         // 检查是否点击了.btnViewItemUp按钮
            //         if (e.target && e.target.matches('.btnViewItemUp')) {
            //             var cCard = e.target.closest('.sortItem');
            //             var tCard = cCard.previousElementSibling;
            //             if (tCard && tCard.matches('.sortItem')) {
            //                 cCard.parentNode.insertBefore(cCard, tCard);
            //                 setButtons();
            //             }
            //         }
            //     });
            // });
        }

        Object.assign(View.prototype, BaseView.prototype);
        View.prototype.onResume = function (options) {
            BaseView.prototype.onResume.apply(this, arguments);
        };

        return View;
    }
);