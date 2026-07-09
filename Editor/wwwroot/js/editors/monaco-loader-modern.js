/**
 * Modern Monaco Editor Loader for Cosmos CMS
 * Replaces unreliable AMD loader with Promise-based loading
 */

class MonacoLoader {
    constructor() {
        this.loaded = false;
        this.loading = false;
        this.loadPromise = null;
        this.monaco = null;
    }

    async load(config = {}) {
        if (this.loaded && this.monaco) {
            return this.monaco;
        }

        if (this.loading) {
            return this.loadPromise;
        }

        this.loading = true;
        
        this.loadPromise = this._loadMonaco(config);
        
        try {
            this.monaco = await this.loadPromise;
            this.loaded = true;
            this.loading = false;
            window.monaco = this.monaco; // Backward compatibility
            return this.monaco;
        } catch (error) {
            this.loading = false;
            console.error('Monaco loading failed:', error);
            throw new Error(`Failed to load Monaco Editor: ${error.message}`);
        }
    }

    async _loadMonaco(config) {
        return new Promise((resolve, reject) => {
            const basePath = config.basePath || '/lib/monaco/min';
            const timeout = config.timeout || 10000;
            
            // Set timeout for loading
            const timeoutId = setTimeout(() => {
                reject(new Error('Monaco Editor loading timeout'));
            }, timeout);

            // Check if loader already exists
            if (window.require?.config) {
                clearTimeout(timeoutId);
                this._requireEditor(basePath, resolve, reject);
                return;
            }

            // Create and load the AMD loader
            const loaderScript = document.createElement('script');
            loaderScript.src = `${basePath}/vs/loader.js`;
            loaderScript.async = true;
            
            loaderScript.onload = () => {
                clearTimeout(timeoutId);
                this._requireEditor(basePath, resolve, reject);
            };

            loaderScript.onerror = () => {
                clearTimeout(timeoutId);
                reject(new Error('Failed to load Monaco loader script'));
            };

            document.head.appendChild(loaderScript);
        });
    }

    _requireEditor(basePath, resolve, reject) {
        const originalDefineProperty = Object.defineProperty;
        const restoreDefineProperty = () => {
            if (Object.defineProperty !== originalDefineProperty) {
                Object.defineProperty = originalDefineProperty;
            }
        };

        const safeDefineProperty = (target, property, descriptor) => {
            if (descriptor === undefined || descriptor === null || typeof descriptor !== 'object') {
                descriptor = {
                    configurable: true,
                    enumerable: true,
                    writable: true,
                    value: descriptor
                };
            }

            return originalDefineProperty(target, property, descriptor);
        };

        try {
            Object.defineProperty = safeDefineProperty;

            window.require.config({
                paths: { vs: `${basePath}/vs` },
                'vs/nls': { availableLanguages: { '*': '' } }
            });

            window.require(['vs/editor/editor.main'], (monacoModule) => {
                restoreDefineProperty();
                const monaco = this._resolveMonacoModule(monacoModule);

                if (monaco && monaco.editor) {
                    resolve(monaco);
                } else {
                    reject(new Error('Monaco editor.main loaded but monaco.editor is undefined. Check if Monaco files are properly deployed.'));
                }
            }, (error) => {
                restoreDefineProperty();
                reject(error);
            });
        } catch (error) {
            restoreDefineProperty();
            reject(error);
        }
    }

    _resolveMonacoModule(monacoModule) {
        const candidates = [
            monacoModule,
            monacoModule?.m,
            monacoModule?.default,
            monacoModule?.default?.m,
            window.monaco
        ];

        for (const candidate of candidates) {
            if (candidate && candidate.editor) {
                return candidate;
            }
        }

        return null;
    }

    async createEditor(container, options = {}) {
        const monaco = await this.load();
        
        if (!container) {
            throw new Error('Editor container element not found');
        }

        if (!monaco || !monaco.editor) {
            throw new Error('Monaco editor API not properly loaded. Ensure Monaco Editor files are correctly deployed.');
        }

        const defaultOptions = {
            theme: 'vs-dark',
            automaticLayout: true,
            minimap: { enabled: true },
            scrollBeyondLastLine: false,
            fontSize: 14,
            wordWrap: 'on',
            formatOnPaste: true,
            formatOnType: true,
            tabSize: 2,
            insertSpaces: true
        };

        return monaco.editor.create(container, {
            ...defaultOptions,
            ...options
        });
    }

    dispose() {
        if (this.monaco && window.monaco) {
            this.monaco = null;
            this.loaded = false;
        }
    }
}

// Export singleton instance
const monacoLoader = new MonacoLoader();
window.monacoLoader = monacoLoader; // Backward compatibility