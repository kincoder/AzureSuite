// Refactored from Microsoft's official Application Insights JavaScript SDK loader
// (https://github.com/microsoft/ApplicationInsights-JS/blob/main/AISKU/snippet/snippet.js)
// into a callable function so each MFE can initialize it with its own connection string
// and "cloud role name" instead of pasting the inline snippet into every index.html.
function loadApplicationInsightsSnippet(win, doc, snipConfig) {
    var locn = win.location;
    var helpLink = "https://go.microsoft.com/fwlink/?linkid=2128109";
    var scriptText = "script";
    var strInstrumentationKey = "instrumentationKey";
    var strIngestionendpoint = "ingestionendpoint";
    var strDisableExceptionTracking = "disableExceptionTracking";
    var strAiDevice = "ai.device.";
    var strAiOperationName = "ai.operation.name";
    var strAiSdkVersion = "ai.internal.sdkVersion";
    var strToLowerCase = "toLowerCase";
    var strConStringIKey = strInstrumentationKey[strToLowerCase]();
    var strEmpty = "";
    var strUndefined = "undefined";
    var strCrossOrigin = "crossOrigin";

    var strPostMethod = "POST";
    var sdkInstanceName = "appInsightsSDK";
    var aiName = snipConfig.name || "appInsights";
    if (snipConfig.name || win[sdkInstanceName]) {
        win[sdkInstanceName] = aiName;
    }
    var aiSdk = win[aiName] || (function (aiConfig) {
        var loadFailed = false;
        var handled = false;
        var appInsights = {
            initialize: true,
            queue: [],
            sv: "10",
            version: 2.0,
            config: aiConfig
        };
        function _parseConnectionString() {
            var fields = {};
            var connectionString = aiConfig.connectionString;
            if (connectionString) {
                var kvPairs = connectionString.split(";");
                for (var lp = 0; lp < kvPairs.length; lp++) {
                    var kvParts = kvPairs[lp].split("=");

                    if (kvParts.length === 2) {
                        fields[kvParts[0][strToLowerCase]()] = kvParts[1];
                    }
                }
            }

            if (!fields[strIngestionendpoint]) {
                var endpointSuffix = fields.endpointsuffix;
                var fLocation = endpointSuffix ? fields.location : null;
                fields[strIngestionendpoint] = "https://" + (fLocation ? fLocation + "." : strEmpty) + "dc." + (endpointSuffix || "services.visualstudio.com");
            }

            return fields;
        }

        function _sendEvents(evts, endpointUrl) {
            if (JSON) {
                var sender = win.fetch;
                if (sender && !snipConfig.useXhr) {
                    sender(endpointUrl, { method: strPostMethod, body: JSON.stringify(evts), mode: "cors" });
                } else if (XMLHttpRequest) {
                    var xhr = new XMLHttpRequest();
                    xhr.open(strPostMethod, endpointUrl);
                    xhr.setRequestHeader("Content-type", "application/json");
                    xhr.send(JSON.stringify(evts));
                }
            }
        }

        function _reportFailure(targetSrc) {
            var conString = _parseConnectionString();
            var iKey = conString[strConStringIKey] || aiConfig[strInstrumentationKey] || strEmpty;
            var ingest = conString[strIngestionendpoint];
            var endpointUrl = ingest ? ingest + "/v2/track" : aiConfig.endpointUrl;

            var message = "SDK LOAD Failure: Failed to load Application Insights SDK script (See stack for details)";
            var evts = [];
            evts.push(_createException(iKey, message, targetSrc, endpointUrl));
            evts.push(_createInternal(iKey, message, targetSrc, endpointUrl));

            _sendEvents(evts, endpointUrl);
        }

        function _getTime() {
            var date = new Date();
            function pad(num) {
                var r = strEmpty + num;
                if (r.length === 1) {
                    r = "0" + r;
                }

                return r;
            }

            return date.getUTCFullYear()
                + "-" + pad(date.getUTCMonth() + 1)
                + "-" + pad(date.getUTCDate())
                + "T" + pad(date.getUTCHours())
                + ":" + pad(date.getUTCMinutes())
                + ":" + pad(date.getUTCSeconds())
                + "." + String((date.getUTCMilliseconds() / 1000).toFixed(3)).slice(2, 5)
                + "Z";
        }

        function _createEnvelope(iKey, theType) {
            var tags = {};
            var type = "Browser";
            tags[strAiDevice + "id"] = type[strToLowerCase]();
            tags[strAiDevice + "type"] = type;
            tags[strAiOperationName] = locn && locn.pathname || "_unknown_";
            tags[strAiSdkVersion] = "javascript:snippet_" + (appInsights.sv || appInsights.version);

            return {
                time: _getTime(),
                iKey: iKey,
                name: "Microsoft.ApplicationInsights." + iKey.replace(/-/g, strEmpty) + "." + theType,
                sampleRate: 100,
                tags: tags,
                data: {
                    baseData: {
                        ver: 2
                    }
                }
            };
        }

        function _createInternal(iKey, message, targetSrc, endpointUrl) {
            var envelope = _createEnvelope(iKey, "Message");
            var data = envelope.data;
            data.baseType = "MessageData";
            var baseData = data.baseData;
            baseData.message = "AI (Internal): 99 message:\"" + (message + " (" + targetSrc + ")").replace(/\"/g, strEmpty) + "\"";
            baseData.properties = {
                endpoint: endpointUrl
            };

            return envelope;
        }

        function _createException(iKey, message, targetSrc, endpointUrl) {
            var envelope = _createEnvelope(iKey, "Exception");
            var data = envelope.data;
            data.baseType = "ExceptionData";
            data.baseData.exceptions = [{
                typeName: "SDKLoadFailed",
                message: message.replace(/\./g, "-"),
                hasFullStack: false,
                stack: message + "\nSnippet failed to load [" + targetSrc + "] -- Telemetry is disabled\nHelp Link: " + helpLink + "\nHost: " + (locn && locn.pathname || "_unknown_") + "\nEndpoint: " + endpointUrl,
                parsedStack: []
            }];

            return envelope;
        }

        var targetSrc = aiConfig.url || snipConfig.src;
        if (targetSrc) {
            function _handleError(evt) {
                loadFailed = true;
                appInsights.queue = [];
                if (!handled) {
                    handled = true;
                    _reportFailure(targetSrc);
                }
            }

            function _handleLoad(evt, isAbort) {
                if (!handled) {
                    setTimeout(function () {
                        if (isAbort || !appInsights.core) {
                            _handleError();
                        }
                    }, 500);
                }
            }

            function _createScript() {
                var scriptElement = doc.createElement(scriptText);
                scriptElement.src = targetSrc;

                var crossOrigin = snipConfig[strCrossOrigin];
                if ((crossOrigin || crossOrigin === "") && scriptElement[strCrossOrigin] != strUndefined) {
                    scriptElement[strCrossOrigin] = crossOrigin;
                }

                scriptElement.onload = _handleLoad;
                scriptElement.onerror = _handleError;

                scriptElement.onreadystatechange = function (evt, isAbort) {
                    if (scriptElement.readyState === "loaded" || scriptElement.readyState === "complete") {
                        _handleLoad(evt, isAbort);
                    }
                };

                return scriptElement;
            }

            var theScript = _createScript();
            if (snipConfig.ld < 0) {
                var headNode = doc.getElementsByTagName("head")[0];
                headNode.appendChild(theScript);
            } else {
                setTimeout(function () {
                    doc.getElementsByTagName(scriptText)[0].parentNode.appendChild(theScript);
                }, snipConfig.ld || 0);
            }
        }

        try {
            appInsights.cookie = doc.cookie;
        } catch (e) { }

        function _createMethods(methods) {
            while (methods.length) {
                (function (name) {
                    appInsights[name] = function () {
                        var originalArguments = arguments;
                        if (!loadFailed) {
                            appInsights.queue.push(function () {
                                appInsights[name].apply(appInsights, originalArguments);
                            });
                        }
                    };
                })(methods.pop());
            }
        }

        var track = "track";
        var trackPage = "TrackPage";
        var trackEvent = "TrackEvent";
        _createMethods([track + "Event",
            track + "PageView",
            track + "Exception",
            track + "Trace",
            track + "DependencyData",
            track + "Metric",
            track + "PageViewPerformance",
            "start" + trackPage,
            "stop" + trackPage,
            "start" + trackEvent,
            "stop" + trackEvent,
            "addTelemetryInitializer",
            "setAuthenticatedUserContext",
            "clearAuthenticatedUserContext",
            "flush"]);

        appInsights['SeverityLevel'] = {
            Verbose: 0,
            Information: 1,
            Warning: 2,
            Error: 3,
            Critical: 4
        };

        var analyticsCfg = ((aiConfig.extensionConfig || {}).ApplicationInsightsAnalytics || {});
        if (!(aiConfig[strDisableExceptionTracking] === true || analyticsCfg[strDisableExceptionTracking] === true)) {
            var method = "onerror";
            _createMethods(["_" + method]);
            var originalOnError = win[method];
            win[method] = function (message, url, lineNumber, columnNumber, error) {
                var handled = originalOnError && originalOnError(message, url, lineNumber, columnNumber, error);
                if (handled !== true) {
                    appInsights["_" + method]({
                        message: message,
                        url: url,
                        lineNumber: lineNumber,
                        columnNumber: columnNumber,
                        error: error,
                        evt: win.event
                    });
                }

                return handled;
            };
            aiConfig.autoExceptionInstrumented = true;
        }

        return appInsights;
    })(snipConfig.cfg);

    win[aiName] = aiSdk;

    function _onInit() {
        if (snipConfig.onInit) {
            snipConfig.onInit(aiSdk);
        }
    }

    if (aiSdk.queue && aiSdk.queue.length === 0) {
        aiSdk.queue.push(_onInit);
        aiSdk.trackPageView({});
    } else {
        _onInit();
    }
}

// Called once per MFE from its own Program.cs (mirrors the backend's
// builder.AddAzureSuiteLogging(serviceName) — each app names itself here too).
function initAppInsights(connectionString, roleName) {
    if (!connectionString) {
        return;
    }

    loadApplicationInsightsSnippet(window, document, {
        src: "https://js.monitor.azure.com/scripts/b/ai.3.gbl.min.js",
        crossOrigin: "anonymous",
        onInit: function (sdk) {
            sdk.addTelemetryInitializer(function (envelope) {
                envelope.tags = envelope.tags || {};
                envelope.tags["ai.cloud.role"] = roleName;
            });
        },
        cfg: {
            connectionString: connectionString
        }
    });
}
