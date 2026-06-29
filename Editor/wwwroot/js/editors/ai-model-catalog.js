(function () {
    function getAiModelSelectionState() {
        if (!window.ccmsAiModelSelection) {
            window.ccmsAiModelSelection = {
                selectedModel: null,
                status: null,
                catalog: null
            };
        }

        return window.ccmsAiModelSelection;
    }

    function getAiCatalogSessionState() {
        if (!window.ccmsAiCatalogSessionState) {
            window.ccmsAiCatalogSessionState = {
                entries: new Map()
            };
        }

        return window.ccmsAiCatalogSessionState;
    }

    function getAiPreferenceContext() {
        const editorContext = window.ccmsEditorContext || {};
        return {
            editorKind: editorContext.editorSurface || 'generic',
            documentKind: editorContext.documentKind || null
        };
    }

    function buildContextKey(options) {
        const editorKind = options && options.editorKind ? options.editorKind : getAiPreferenceContext().editorKind;
        const documentKind = options && Object.prototype.hasOwnProperty.call(options, 'documentKind')
            ? options.documentKind
            : getAiPreferenceContext().documentKind;
        const providerKey = options && options.providerKey ? options.providerKey : 'unknown';
        return [providerKey, editorKind || '', documentKind || ''].join('|').toLowerCase();
    }

    function getModelId(model) {
        return (model && (model.id ?? model.Id)) || '';
    }

    function getModelDisplayName(model) {
        const modelId = getModelId(model);
        return (model && (model.displayName ?? model.DisplayName)) || modelId;
    }

    function buildPreferenceQueryString(context) {
        const params = new URLSearchParams();
        if (context.editorKind) {
            params.set('editorKind', context.editorKind);
        }

        if (context.documentKind) {
            params.set('documentKind', context.documentKind);
        }

        return params.toString();
    }

    function isSelectionSupported(catalog) {
        return !!(catalog && (catalog.supportsUserModelSelection ?? catalog.SupportsUserModelSelection));
    }

    function getCatalogModels(catalog) {
        return catalog && (catalog.models ?? catalog.Models) ? (catalog.models ?? catalog.Models) : [];
    }

    async function normalizeSelectedModelAgainstCatalog(catalog, savePreference) {
        const state = getAiModelSelectionState();
        const supportsSelection = isSelectionSupported(catalog);
        const models = getCatalogModels(catalog);

        if (!supportsSelection || !state.selectedModel) {
            if (!supportsSelection && state.selectedModel) {
                state.selectedModel = null;
                if (savePreference) {
                    await savePreference(null);
                }
            }

            return state.selectedModel;
        }

        const selectionExists = models.some((model) => {
            const modelId = getModelId(model);
            return modelId.toLowerCase() === state.selectedModel.toLowerCase();
        });

        if (!selectionExists) {
            state.selectedModel = null;
            if (savePreference) {
                await savePreference(null);
            }
        }

        return state.selectedModel;
    }

    async function fetchJsonWithCache(url, cacheKey, forceRefresh) {
        const sessionState = getAiCatalogSessionState();
        const cachedEntry = sessionState.entries.get(cacheKey);
        if (cachedEntry && !forceRefresh) {
            return cachedEntry.promise;
        }

        const fetchPromise = fetch(url, {
            method: 'GET',
            credentials: 'same-origin',
            headers: {
                Accept: 'application/json'
            }
        })
            .then(async (response) => {
                if (!response.ok) {
                    return null;
                }

                return await response.json();
            })
            .catch(() => null)
            .finally(() => {
                const currentEntry = sessionState.entries.get(cacheKey);
                if (currentEntry && currentEntry.promise === fetchPromise) {
                    currentEntry.promise = null;
                }
            });

        sessionState.entries.set(cacheKey, {
            promise: fetchPromise,
            value: cachedEntry ? cachedEntry.value : null,
            updatedAt: Date.now()
        });

        const result = await fetchPromise;
        sessionState.entries.set(cacheKey, {
            promise: null,
            value: result,
            updatedAt: Date.now()
        });

        return result;
    }

    async function getStatus(options) {
        const context = options || getAiPreferenceContext();
        const queryString = buildPreferenceQueryString(context);
        const cacheKey = `status:${buildContextKey({
            providerKey: options && options.providerKey ? options.providerKey : 'status',
            editorKind: context.editorKind,
            documentKind: context.documentKind
        })}`;
        const url = `/api/ai-proxy/status${queryString ? `?${queryString}` : ''}`;
        const status = await fetchJsonWithCache(url, cacheKey, !!(options && options.forceRefresh));
        if (status) {
            const state = getAiModelSelectionState();
            state.status = status;
            state.selectedModel = status.selectedModel ?? status.SelectedModel ?? state.selectedModel ?? null;
        }

        return status;
    }

    async function saveSelectedModelPreference(selectedModel, options) {
        try {
            const context = options && options.context ? options.context : getAiPreferenceContext();
            const response = await fetch('/api/ai-proxy/preferences/model', {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    Accept: 'application/json'
                },
                body: JSON.stringify({
                    editorKind: context.editorKind,
                    documentKind: context.documentKind,
                    selectedModel: selectedModel
                })
            });

            return response.ok;
        } catch {
            return false;
        }
    }

    async function getCatalog(options) {
        const context = options && options.context ? options.context : getAiPreferenceContext();
        const forceRefresh = !!(options && options.forceRefresh);
        const status = await getStatus({
            context: context,
            forceRefresh: false
        });
        const cacheKey = `catalog:${buildContextKey({
            providerKey: status && (status.providerKey ?? status.ProviderKey)
                ? (status.providerKey ?? status.ProviderKey)
                : (options && options.providerKey ? options.providerKey : 'catalog'),
            editorKind: context.editorKind,
            documentKind: context.documentKind
        })}`;
        const query = new URLSearchParams(buildPreferenceQueryString(context));
        if (forceRefresh) {
            query.set('forceRefresh', 'true');
        }

        const url = `/api/ai-proxy/models${query.toString() ? `?${query.toString()}` : ''}`;
        const catalog = await fetchJsonWithCache(url, cacheKey, forceRefresh);
        const state = getAiModelSelectionState();
        state.catalog = catalog;

        if (catalog) {
            const selectedModel = catalog.selectedModel ?? catalog.SelectedModel;
            if (selectedModel !== undefined) {
                state.selectedModel = selectedModel || null;
            }
        }

        await normalizeSelectedModelAgainstCatalog(catalog, (selectedModel) => saveSelectedModelPreference(selectedModel, { context }));
        return catalog;
    }

    function clearCatalogCache(options) {
        const context = options && options.context ? options.context : getAiPreferenceContext();
        const sessionState = getAiCatalogSessionState();
        const prefix = buildContextKey({
            providerKey: options && options.providerKey ? options.providerKey : '',
            editorKind: context.editorKind,
            documentKind: context.documentKind
        });

        Array.from(sessionState.entries.keys()).forEach((key) => {
            if (key.includes(prefix)) {
                sessionState.entries.delete(key);
            }
        });
    }

    function getDisplayedModel() {
        const state = getAiModelSelectionState();
        if (state.selectedModel) {
            return state.selectedModel;
        }

        return state.status && ((state.status.effectiveModel ?? state.status.EffectiveModel) || (state.status.model ?? state.status.Model));
    }

    function getSelectedModel() {
        return getAiModelSelectionState().selectedModel || null;
    }

    function updateModelSelectionFromCatalog(catalog, savePreference) {
        return normalizeSelectedModelAgainstCatalog(catalog, savePreference);
    }

    window.ccmsAiModelCatalog = {
        getAiModelSelectionState,
        getAiPreferenceContext,
        buildPreferenceQueryString,
        getStatus,
        getCatalog,
        saveSelectedModelPreference,
        clearCatalogCache,
        updateModelSelectionFromCatalog,
        getDisplayedModel,
        getSelectedModel,
        isSelectionSupported,
        getCatalogModels,
        getModelId,
        getModelDisplayName,
        normalizeSelectedModelAgainstCatalog
    };
})();
