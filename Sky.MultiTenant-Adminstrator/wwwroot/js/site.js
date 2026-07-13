// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Database connection builder for the Connections Create/Edit views.
//
// Markup contract:
// - a modal with id="dbConnectionBuilderModal"
// - launch buttons with data-db-connection-builder and data-target-input
// - provider sections and form fields identified by the data-* attributes used below
//
// Public API:
// - window.SkyDbConnectionBuilder in the browser
// - module.exports in Jest tests
(function () {
    const providerLabels = {
        cosmos: "Azure Cosmos DB",
        sqlserver: "SQL Server",
        mysql: "MySQL",
        sqlite: "SQLite"
    };

    const storageProviderLabels = {
        azure: "Azure Blob Storage",
        s3: "Amazon S3",
        r2: "Cloudflare R2"
    };

    // State for the currently open modal session. This is reset whenever the modal closes.
    const state = {
        activeInput: null,
        provider: null,
        extraSegments: [],
        unsupported: false,
        modal: null,
        modalElement: null
    };

    // Separate state for storage connection builder interactions.
    const storageState = {
        activeInput: null,
        provider: null,
        extraSegments: [],
        unsupported: false,
        modal: null,
        modalElement: null
    };

    // Wires the builder to the shared modal and to every launch button on the page.
    // Safe to call multiple times in tests because the page DOM is recreated per test.
    function initializeDatabaseConnectionBuilder() {
        const modalElement = document.getElementById("dbConnectionBuilderModal");
        if (!modalElement || !window.bootstrap) {
            return;
        }

        state.modalElement = modalElement;
        state.modal = window.bootstrap.Modal.getOrCreateInstance(modalElement);

        document.querySelectorAll("[data-db-connection-builder]").forEach((button) => {
            button.addEventListener("click", onBuilderOpen);
        });

        modalElement.querySelectorAll("[data-provider-option]").forEach((button) => {
            button.addEventListener("click", () => selectProvider(button.getAttribute("data-provider-option")));
        });

        modalElement.querySelector("[data-db-builder-save]").addEventListener("click", onSaveClick);
        modalElement.querySelector("[data-db-builder-clear]").addEventListener("click", onClearClick);
        modalElement.querySelector("#dbBuilderSqlServerAuthentication").addEventListener("change", toggleSqlServerAuthenticationFields);
        modalElement.addEventListener("hidden.bs.modal", resetBuilderState);
    }

    // Opens the modal for the clicked DbConn input. Existing values are detected,
    // parsed, and mapped back into the matching provider form when possible.
    function onBuilderOpen(event) {
        const targetInputId = event.currentTarget.getAttribute("data-target-input");
        const input = document.getElementById(targetInputId);
        if (!input) {
            return;
        }

        state.activeInput = input;
        resetBuilderUi();

        const connectionString = (input.value || "").trim();
        if (!connectionString) {
            showProviderSelection();
            state.modal.show();
            return;
        }

        const provider = detectProvider(connectionString);
        if (!provider) {
            showUnsupportedState();
            state.modal.show();
            return;
        }

        const parsed = parseConnectionString(connectionString, provider);
        if (!parsed) {
            showUnsupportedState();
            state.modal.show();
            return;
        }

        state.provider = provider;
        state.extraSegments = parsed.extraSegments;
        populateProviderFields(provider, parsed.fields);
        showProviderForm(provider);
        state.modal.show();
    }

    function resetBuilderState() {
        state.activeInput = null;
        resetBuilderUi();
    }

    function resetBuilderUi() {
        state.provider = null;
        state.extraSegments = [];
        state.unsupported = false;
        clearValidation();
        clearAllProviderFields();
        toggleSqlServerAuthenticationFields();
        document.querySelector("[data-db-builder-provider-selection]").classList.remove("d-none");
        document.querySelector("[data-db-builder-provider-forms]").classList.add("d-none");
        document.querySelector("[data-db-builder-unsupported]").classList.add("d-none");
        document.getElementById("dbConnectionBuilderModalLabel").textContent = "Database Connection Builder";
    }

    function showProviderSelection() {
        clearValidation();
        state.provider = null;
        state.extraSegments = [];
        state.unsupported = false;
        document.querySelector("[data-db-builder-provider-selection]").classList.remove("d-none");
        document.querySelector("[data-db-builder-provider-forms]").classList.add("d-none");
        document.querySelector("[data-db-builder-unsupported]").classList.add("d-none");
        document.getElementById("dbConnectionBuilderModalLabel").textContent = "Database Connection Builder";
    }

    function showProviderForm(provider) {
        clearValidation();
        state.provider = provider;
        state.unsupported = false;
        document.querySelector("[data-db-builder-provider-selection]").classList.add("d-none");
        document.querySelector("[data-db-builder-provider-forms]").classList.remove("d-none");
        document.querySelector("[data-db-builder-unsupported]").classList.add("d-none");

        document.querySelectorAll("[data-provider-form]").forEach((form) => {
            form.classList.toggle("d-none", form.getAttribute("data-provider-form") !== provider);
        });

        document.querySelector("[data-db-builder-provider-label]").textContent = providerLabels[provider];
        document.getElementById("dbConnectionBuilderModalLabel").textContent = providerLabels[provider] + " Connection";
        toggleSqlServerAuthenticationFields();
    }

    function showUnsupportedState() {
        clearValidation();
        state.provider = null;
        state.extraSegments = [];
        state.unsupported = true;
        clearAllProviderFields();
        document.querySelector("[data-db-builder-provider-selection]").classList.add("d-none");
        document.querySelector("[data-db-builder-provider-forms]").classList.add("d-none");
        document.querySelector("[data-db-builder-unsupported]").classList.remove("d-none");
        document.getElementById("dbConnectionBuilderModalLabel").textContent = "Unsupported Connection String";
    }

    function onClearClick() {
        resetBuilderUi();
        showProviderSelection();
    }

    function onSaveClick() {
        if (!state.activeInput) {
            return;
        }

        if (state.unsupported) {
            showValidation("Use Clear to remove the existing connection string, or Cancel to keep it unchanged.");
            return;
        }

        if (!state.provider) {
            if (confirmEmptyConnectionString()) {
                setInputValue(state.activeInput, "");
                state.modal.hide();
            }

            return;
        }

        const fields = collectProviderFields(state.provider);
        if (areAllFieldsBlank(state.provider, fields)) {
            if (confirmEmptyConnectionString()) {
                setInputValue(state.activeInput, "");
                state.modal.hide();
            }

            return;
        }

        const validationErrors = validateProviderFields(state.provider, fields);
        if (validationErrors.length > 0) {
            showValidation(validationErrors.join(" "));
            return;
        }

        const connectionString = buildConnectionString(state.provider, fields, state.extraSegments);
        setInputValue(state.activeInput, connectionString);
        state.modal.hide();
    }

    function confirmEmptyConnectionString() {
        return window.confirm("No database connection data was provided. Select OK to save an empty database connection string and remove any existing value.");
    }

    function setInputValue(input, value) {
        input.value = value;
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function clearValidation() {
        const validationElement = document.querySelector("[data-db-builder-validation]");
        validationElement.textContent = "";
        validationElement.classList.add("d-none");
    }

    function showValidation(message) {
        const validationElement = document.querySelector("[data-db-builder-validation]");
        validationElement.textContent = message;
        validationElement.classList.remove("d-none");
    }

    function clearAllProviderFields() {
        document.getElementById("dbBuilderCosmosAccountEndpoint").value = "";
        document.getElementById("dbBuilderCosmosDatabase").value = "";
        document.getElementById("dbBuilderCosmosAccountKey").value = "";

        document.getElementById("dbBuilderSqlServerServer").value = "";
        document.getElementById("dbBuilderSqlServerDatabase").value = "";
        document.getElementById("dbBuilderSqlServerAuthentication").value = "Sql";
        document.getElementById("dbBuilderSqlServerUserId").value = "";
        document.getElementById("dbBuilderSqlServerPassword").value = "";

        document.getElementById("dbBuilderMySqlServer").value = "";
        document.getElementById("dbBuilderMySqlPort").value = "3306";
        document.getElementById("dbBuilderMySqlDatabase").value = "";
        document.getElementById("dbBuilderMySqlUserId").value = "";
        document.getElementById("dbBuilderMySqlPassword").value = "";

        document.getElementById("dbBuilderSqliteDataSource").value = "";
        document.getElementById("dbBuilderSqlitePassword").value = "";
    }

    function toggleSqlServerAuthenticationFields() {
        const authentication = document.getElementById("dbBuilderSqlServerAuthentication").value;
        const showSqlAuthenticationFields = authentication !== "Integrated";

        document.querySelectorAll("[data-sql-auth-fields]").forEach((element) => {
            element.classList.toggle("d-none", !showSqlAuthenticationFields);
        });
    }

    function selectProvider(provider) {
        clearAllProviderFields();
        if (provider === "mysql") {
            document.getElementById("dbBuilderMySqlPort").value = "3306";
        }

        showProviderForm(provider);
    }

    // Infers which provider form should be used based on the same broad patterns
    // supported by FlexDb and the widget UI.
    function detectProvider(connectionString) {
        const normalized = connectionString.toLowerCase();
        if (normalized.includes("accountendpoint=")) {
            return "cosmos";
        }

        if (normalized.includes("uid=") || (normalized.includes("port=") && normalized.includes("database=") && normalized.includes("user id="))) {
            return "mysql";
        }

        if (normalized.includes("user id=") || normalized.includes("trusted_connection") || normalized.includes("integrated security")) {
            return "sqlserver";
        }

        if (normalized.includes("data source=") && (normalized.includes(":memory:") || normalized.includes(".db") || normalized.includes(".sqlite"))) {
            return "sqlite";
        }

        return null;
    }

    // Breaks a connection string into widget fields for the selected provider and
    // preserves any segments not modeled by the current UI so they can be re-applied on save.
    function parseConnectionString(connectionString, provider) {
        const parts = splitConnectionString(connectionString);
        const normalizedParts = parts.map(createConnectionPart);

        switch (provider) {
            case "cosmos":
                return {
                    fields: {
                        accountEndpoint: getValue(normalizedParts, ["accountendpoint"]),
                        database: getValue(normalizedParts, ["database"]),
                        accountKey: getValue(normalizedParts, ["accountkey"])
                    },
                    extraSegments: getExtraSegments(normalizedParts, ["accountendpoint", "database", "accountkey"])
                };
            case "sqlserver":
                return {
                    fields: {
                        server: getValue(normalizedParts, ["server", "data source"]),
                        database: getValue(normalizedParts, ["initial catalog", "database"]),
                        authentication: isIntegratedSecurity(normalizedParts) ? "Integrated" : "Sql",
                        userId: getValue(normalizedParts, ["user id", "uid"]),
                        password: getValue(normalizedParts, ["password", "pwd"])
                    },
                    extraSegments: getExtraSegments(normalizedParts, ["server", "data source", "initial catalog", "database", "user id", "uid", "password", "pwd", "integrated security", "trusted_connection"])
                };
            case "mysql":
                return {
                    fields: {
                        server: getValue(normalizedParts, ["server"]),
                        port: getValue(normalizedParts, ["port"]) || "3306",
                        database: getValue(normalizedParts, ["database"]),
                        userId: getValue(normalizedParts, ["uid", "user id"]),
                        password: getValue(normalizedParts, ["pwd", "password"])
                    },
                    extraSegments: getExtraSegments(normalizedParts, ["server", "port", "database", "uid", "user id", "pwd", "password"])
                };
            case "sqlite":
                return {
                    fields: {
                        dataSource: getValue(normalizedParts, ["data source"]),
                        password: getValue(normalizedParts, ["password"])
                    },
                    extraSegments: getExtraSegments(normalizedParts, ["data source", "password"])
                };
            default:
                return null;
        }
    }

    function splitConnectionString(connectionString) {
        return connectionString
            .split(";")
            .map((part) => part.trim())
            .filter((part) => part.length > 0);
    }

    function createConnectionPart(part) {
        const separatorIndex = part.indexOf("=");
        if (separatorIndex < 0) {
            return {
                raw: part,
                key: part,
                normalizedKey: part.toLowerCase(),
                value: ""
            };
        }

        return {
            raw: part,
            key: part.substring(0, separatorIndex).trim(),
            normalizedKey: part.substring(0, separatorIndex).trim().toLowerCase(),
            value: part.substring(separatorIndex + 1).trim()
        };
    }

    function getValue(parts, keys) {
        const normalizedKeys = keys.map((key) => key.toLowerCase());
        const matchingPart = parts.find((part) => normalizedKeys.includes(part.normalizedKey));
        return matchingPart ? matchingPart.value : "";
    }

    function getExtraSegments(parts, knownKeys) {
        const normalizedKeys = knownKeys.map((key) => key.toLowerCase());
        return parts
            .filter((part) => !normalizedKeys.includes(part.normalizedKey))
            .map((part) => part.raw);
    }

    function isIntegratedSecurity(parts) {
        const integratedValue = getValue(parts, ["integrated security", "trusted_connection"]);
        return ["true", "sspi", "yes"].includes((integratedValue || "").toLowerCase());
    }

    function populateProviderFields(provider, fields) {
        clearAllProviderFields();

        switch (provider) {
            case "cosmos":
                document.getElementById("dbBuilderCosmosAccountEndpoint").value = fields.accountEndpoint || "";
                document.getElementById("dbBuilderCosmosDatabase").value = fields.database || "";
                document.getElementById("dbBuilderCosmosAccountKey").value = fields.accountKey || "";
                break;
            case "sqlserver":
                document.getElementById("dbBuilderSqlServerServer").value = fields.server || "";
                document.getElementById("dbBuilderSqlServerDatabase").value = fields.database || "";
                document.getElementById("dbBuilderSqlServerAuthentication").value = fields.authentication || "Sql";
                document.getElementById("dbBuilderSqlServerUserId").value = fields.userId || "";
                document.getElementById("dbBuilderSqlServerPassword").value = fields.password || "";
                break;
            case "mysql":
                document.getElementById("dbBuilderMySqlServer").value = fields.server || "";
                document.getElementById("dbBuilderMySqlPort").value = fields.port || "3306";
                document.getElementById("dbBuilderMySqlDatabase").value = fields.database || "";
                document.getElementById("dbBuilderMySqlUserId").value = fields.userId || "";
                document.getElementById("dbBuilderMySqlPassword").value = fields.password || "";
                break;
            case "sqlite":
                document.getElementById("dbBuilderSqliteDataSource").value = fields.dataSource || "";
                document.getElementById("dbBuilderSqlitePassword").value = fields.password || "";
                break;
            default:
                break;
        }
    }

    function collectProviderFields(provider) {
        switch (provider) {
            case "cosmos":
                return {
                    accountEndpoint: document.getElementById("dbBuilderCosmosAccountEndpoint").value.trim(),
                    database: document.getElementById("dbBuilderCosmosDatabase").value.trim(),
                    accountKey: document.getElementById("dbBuilderCosmosAccountKey").value.trim()
                };
            case "sqlserver":
                return {
                    server: document.getElementById("dbBuilderSqlServerServer").value.trim(),
                    database: document.getElementById("dbBuilderSqlServerDatabase").value.trim(),
                    authentication: document.getElementById("dbBuilderSqlServerAuthentication").value,
                    userId: document.getElementById("dbBuilderSqlServerUserId").value.trim(),
                    password: document.getElementById("dbBuilderSqlServerPassword").value.trim()
                };
            case "mysql":
                return {
                    server: document.getElementById("dbBuilderMySqlServer").value.trim(),
                    port: document.getElementById("dbBuilderMySqlPort").value.trim(),
                    database: document.getElementById("dbBuilderMySqlDatabase").value.trim(),
                    userId: document.getElementById("dbBuilderMySqlUserId").value.trim(),
                    password: document.getElementById("dbBuilderMySqlPassword").value.trim()
                };
            case "sqlite":
                return {
                    dataSource: document.getElementById("dbBuilderSqliteDataSource").value.trim(),
                    password: document.getElementById("dbBuilderSqlitePassword").value.trim()
                };
            default:
                return {};
        }
    }

    function areAllFieldsBlank(provider, fields) {
        switch (provider) {
            case "cosmos":
                return !fields.accountEndpoint && !fields.database && !fields.accountKey;
            case "sqlserver":
                if (fields.authentication === "Integrated") {
                    return !fields.server && !fields.database;
                }

                return !fields.server && !fields.database && !fields.userId && !fields.password;
            case "mysql":
                return !fields.server && !fields.database && !fields.userId && !fields.password;
            case "sqlite":
                return !fields.dataSource && !fields.password;
            default:
                return true;
        }
    }

    // Applies the widget's required-field rules before building a connection string.
    // Validation is intentionally limited to the fields exposed by the v1 UI.
    function validateProviderFields(provider, fields) {
        const errors = [];

        switch (provider) {
            case "cosmos":
                if (!fields.accountEndpoint || !fields.database || !fields.accountKey) {
                    errors.push("Account Endpoint, Database, and Account Key or AccessToken are required for Azure Cosmos DB.");
                }

                break;
            case "sqlserver":
                if (!fields.server || !fields.database) {
                    errors.push("Server and Database are required for SQL Server.");
                }

                if (fields.authentication !== "Integrated" && (!fields.userId || !fields.password)) {
                    errors.push("User ID and Password are required for SQL Server Authentication.");
                }

                break;
            case "mysql":
                if (!fields.server || !fields.database || !fields.userId || !fields.password) {
                    errors.push("Server, Database, User ID, and Password are required for MySQL.");
                }

                if (fields.port && !/^\d+$/.test(fields.port)) {
                    errors.push("Port must be numeric for MySQL.");
                }

                break;
            case "sqlite":
                if (!fields.dataSource) {
                    errors.push("Data Source is required for SQLite.");
                }

                break;
            default:
                break;
        }

        return errors;
    }

    // Reassembles the normalized provider connection string and appends any preserved
    // extra segments from the original value.
    function buildConnectionString(provider, fields, extraSegments) {
        const segments = [];

        switch (provider) {
            case "cosmos":
                segments.push("AccountEndpoint=" + fields.accountEndpoint);
                segments.push("AccountKey=" + fields.accountKey);
                segments.push("Database=" + fields.database);
                break;
            case "sqlserver":
                segments.push("Server=" + fields.server);
                segments.push("Initial Catalog=" + fields.database);
                if (fields.authentication === "Integrated") {
                    segments.push("Integrated Security=True");
                }
                else {
                    segments.push("User ID=" + fields.userId);
                    segments.push("Password=" + fields.password);
                }

                break;
            case "mysql":
                segments.push("Server=" + fields.server);
                segments.push("Port=" + (fields.port || "3306"));
                segments.push("uid=" + fields.userId);
                segments.push("pwd=" + fields.password);
                segments.push("database=" + fields.database);
                break;
            case "sqlite":
                segments.push("Data Source=" + fields.dataSource);
                if (fields.password) {
                    segments.push("Password=" + fields.password);
                }

                break;
            default:
                break;
        }

        return segments.concat(extraSegments || []).join(";") + ";";
    }

    function initializeStorageConnectionBuilder() {
        const modalElement = document.getElementById("storageConnectionBuilderModal");
        if (!modalElement || !window.bootstrap) {
            return;
        }

        storageState.modalElement = modalElement;
        storageState.modal = window.bootstrap.Modal.getOrCreateInstance(modalElement);

        document.querySelectorAll("[data-storage-connection-builder]").forEach((button) => {
            button.addEventListener("click", onStorageBuilderOpen);
        });

        modalElement.querySelectorAll("[data-storage-provider-option]").forEach((button) => {
            button.addEventListener("click", () => selectStorageProvider(button.getAttribute("data-storage-provider-option")));
        });

        modalElement.querySelector("[data-storage-builder-save]").addEventListener("click", onStorageSaveClick);
        modalElement.querySelector("[data-storage-builder-clear]").addEventListener("click", onStorageClearClick);
        modalElement.addEventListener("hidden.bs.modal", resetStorageBuilderState);
    }

    function onStorageBuilderOpen(event) {
        const targetInputId = event.currentTarget.getAttribute("data-target-input");
        const input = document.getElementById(targetInputId);
        if (!input) {
            return;
        }

        storageState.activeInput = input;
        resetStorageBuilderUi();

        const connectionString = (input.value || "").trim();
        if (!connectionString) {
            showStorageProviderSelection();
            storageState.modal.show();
            return;
        }

        const provider = detectStorageProvider(connectionString);
        if (!provider) {
            showUnsupportedStorageState();
            storageState.modal.show();
            return;
        }

        const parsed = parseStorageConnectionString(connectionString, provider);
        if (!parsed) {
            showUnsupportedStorageState();
            storageState.modal.show();
            return;
        }

        storageState.provider = provider;
        storageState.extraSegments = parsed.extraSegments;
        populateStorageProviderFields(provider, parsed.fields);
        showStorageProviderForm(provider);
        storageState.modal.show();
    }

    function resetStorageBuilderState() {
        storageState.activeInput = null;
        resetStorageBuilderUi();
    }

    function resetStorageBuilderUi() {
        storageState.provider = null;
        storageState.extraSegments = [];
        storageState.unsupported = false;
        clearStorageValidation();
        clearAllStorageProviderFields();
        document.querySelector("[data-storage-builder-provider-selection]").classList.remove("d-none");
        document.querySelector("[data-storage-builder-provider-forms]").classList.add("d-none");
        document.querySelector("[data-storage-builder-unsupported]").classList.add("d-none");
        document.getElementById("storageConnectionBuilderModalLabel").textContent = "Storage Connection Builder";
    }

    function showStorageProviderSelection() {
        clearStorageValidation();
        storageState.provider = null;
        storageState.extraSegments = [];
        storageState.unsupported = false;
        document.querySelector("[data-storage-builder-provider-selection]").classList.remove("d-none");
        document.querySelector("[data-storage-builder-provider-forms]").classList.add("d-none");
        document.querySelector("[data-storage-builder-unsupported]").classList.add("d-none");
        document.getElementById("storageConnectionBuilderModalLabel").textContent = "Storage Connection Builder";
    }

    function showStorageProviderForm(provider) {
        clearStorageValidation();
        storageState.provider = provider;
        storageState.unsupported = false;
        document.querySelector("[data-storage-builder-provider-selection]").classList.add("d-none");
        document.querySelector("[data-storage-builder-provider-forms]").classList.remove("d-none");
        document.querySelector("[data-storage-builder-unsupported]").classList.add("d-none");

        document.querySelectorAll("[data-storage-provider-form]").forEach((form) => {
            form.classList.toggle("d-none", form.getAttribute("data-storage-provider-form") !== provider);
        });

        document.querySelector("[data-storage-builder-provider-label]").textContent = storageProviderLabels[provider];
        document.getElementById("storageConnectionBuilderModalLabel").textContent = storageProviderLabels[provider] + " Connection";
    }

    function showUnsupportedStorageState() {
        clearStorageValidation();
        storageState.provider = null;
        storageState.extraSegments = [];
        storageState.unsupported = true;
        clearAllStorageProviderFields();
        document.querySelector("[data-storage-builder-provider-selection]").classList.add("d-none");
        document.querySelector("[data-storage-builder-provider-forms]").classList.add("d-none");
        document.querySelector("[data-storage-builder-unsupported]").classList.remove("d-none");
        document.getElementById("storageConnectionBuilderModalLabel").textContent = "Unsupported Storage Connection String";
    }

    function onStorageClearClick() {
        resetStorageBuilderUi();
        showStorageProviderSelection();
    }

    function onStorageSaveClick() {
        if (!storageState.activeInput) {
            return;
        }

        if (storageState.unsupported) {
            showStorageValidation("Use Clear to remove the existing storage connection string, or Cancel to keep it unchanged.");
            return;
        }

        if (!storageState.provider) {
            if (confirmEmptyStorageConnectionString()) {
                setInputValue(storageState.activeInput, "");
                storageState.modal.hide();
            }

            return;
        }

        const fields = collectStorageProviderFields(storageState.provider);
        if (areAllStorageFieldsBlank(storageState.provider, fields)) {
            if (confirmEmptyStorageConnectionString()) {
                setInputValue(storageState.activeInput, "");
                storageState.modal.hide();
            }

            return;
        }

        const validationErrors = validateStorageProviderFields(storageState.provider, fields);
        if (validationErrors.length > 0) {
            showStorageValidation(validationErrors.join(" "));
            return;
        }

        const connectionString = buildStorageConnectionString(storageState.provider, fields, storageState.extraSegments);
        setInputValue(storageState.activeInput, connectionString);
        storageState.modal.hide();
    }

    function confirmEmptyStorageConnectionString() {
        return window.confirm("No storage connection data was provided. Select OK to save an empty storage connection string and remove any existing value.");
    }

    function clearStorageValidation() {
        const validationElement = document.querySelector("[data-storage-builder-validation]");
        validationElement.textContent = "";
        validationElement.classList.add("d-none");
    }

    function showStorageValidation(message) {
        const validationElement = document.querySelector("[data-storage-builder-validation]");
        validationElement.textContent = message;
        validationElement.classList.remove("d-none");
    }

    function clearAllStorageProviderFields() {
        document.getElementById("storageBuilderAzureProtocol").value = "https";
        document.getElementById("storageBuilderAzureAccountName").value = "";
        document.getElementById("storageBuilderAzureEndpointSuffix").value = "core.windows.net";
        document.getElementById("storageBuilderAzureAccountKey").value = "";

        document.getElementById("storageBuilderS3Bucket").value = "";
        document.getElementById("storageBuilderS3Region").value = "";
        document.getElementById("storageBuilderS3KeyId").value = "";
        document.getElementById("storageBuilderS3Key").value = "";

        document.getElementById("storageBuilderR2Bucket").value = "";
        document.getElementById("storageBuilderR2AccountId").value = "";
        document.getElementById("storageBuilderR2KeyId").value = "";
        document.getElementById("storageBuilderR2Key").value = "";
    }

    function selectStorageProvider(provider) {
        clearAllStorageProviderFields();
        showStorageProviderForm(provider);
    }

    function detectStorageProvider(connectionString) {
        const normalized = connectionString.toLowerCase();
        if (normalized.startsWith("defaultendpointsprotocol=")) {
            return "azure";
        }

        if (normalized.includes("accountid") && normalized.includes("bucket")) {
            return "r2";
        }

        if (normalized.includes("bucket") && normalized.includes("region")) {
            return "s3";
        }

        return null;
    }

    function parseStorageConnectionString(connectionString, provider) {
        const parts = splitConnectionString(connectionString);
        const normalizedParts = parts.map(createConnectionPart);

        switch (provider) {
            case "azure":
                return {
                    fields: {
                        protocol: getValue(normalizedParts, ["defaultendpointsprotocol"]),
                        accountName: getValue(normalizedParts, ["accountname"]),
                        accountKey: getValue(normalizedParts, ["accountkey"]),
                        endpointSuffix: getValue(normalizedParts, ["endpointsuffix"]) || "core.windows.net"
                    },
                    extraSegments: getExtraSegments(normalizedParts, ["defaultendpointsprotocol", "accountname", "accountkey", "endpointsuffix"])
                };
            case "s3":
                return {
                    fields: {
                        bucket: getValue(normalizedParts, ["bucket"]),
                        region: getValue(normalizedParts, ["region"]),
                        keyId: getValue(normalizedParts, ["keyid"]),
                        key: getValue(normalizedParts, ["key"])
                    },
                    extraSegments: getExtraSegments(normalizedParts, ["bucket", "region", "keyid", "key", "accountid"])
                };
            case "r2":
                return {
                    fields: {
                        bucket: getValue(normalizedParts, ["bucket"]),
                        accountId: getValue(normalizedParts, ["accountid"]),
                        keyId: getValue(normalizedParts, ["keyid"]),
                        key: getValue(normalizedParts, ["key"])
                    },
                    extraSegments: getExtraSegments(normalizedParts, ["bucket", "accountid", "keyid", "key", "region"])
                };
            default:
                return null;
        }
    }

    function populateStorageProviderFields(provider, fields) {
        clearAllStorageProviderFields();

        switch (provider) {
            case "azure":
                document.getElementById("storageBuilderAzureProtocol").value = fields.protocol || "https";
                document.getElementById("storageBuilderAzureAccountName").value = fields.accountName || "";
                document.getElementById("storageBuilderAzureAccountKey").value = fields.accountKey || "";
                document.getElementById("storageBuilderAzureEndpointSuffix").value = fields.endpointSuffix || "core.windows.net";
                break;
            case "s3":
                document.getElementById("storageBuilderS3Bucket").value = fields.bucket || "";
                document.getElementById("storageBuilderS3Region").value = fields.region || "";
                document.getElementById("storageBuilderS3KeyId").value = fields.keyId || "";
                document.getElementById("storageBuilderS3Key").value = fields.key || "";
                break;
            case "r2":
                document.getElementById("storageBuilderR2Bucket").value = fields.bucket || "";
                document.getElementById("storageBuilderR2AccountId").value = fields.accountId || "";
                document.getElementById("storageBuilderR2KeyId").value = fields.keyId || "";
                document.getElementById("storageBuilderR2Key").value = fields.key || "";
                break;
            default:
                break;
        }
    }

    function collectStorageProviderFields(provider) {
        switch (provider) {
            case "azure":
                return {
                    protocol: document.getElementById("storageBuilderAzureProtocol").value.trim(),
                    accountName: document.getElementById("storageBuilderAzureAccountName").value.trim(),
                    accountKey: document.getElementById("storageBuilderAzureAccountKey").value.trim(),
                    endpointSuffix: document.getElementById("storageBuilderAzureEndpointSuffix").value.trim()
                };
            case "s3":
                return {
                    bucket: document.getElementById("storageBuilderS3Bucket").value.trim(),
                    region: document.getElementById("storageBuilderS3Region").value.trim(),
                    keyId: document.getElementById("storageBuilderS3KeyId").value.trim(),
                    key: document.getElementById("storageBuilderS3Key").value.trim()
                };
            case "r2":
                return {
                    bucket: document.getElementById("storageBuilderR2Bucket").value.trim(),
                    accountId: document.getElementById("storageBuilderR2AccountId").value.trim(),
                    keyId: document.getElementById("storageBuilderR2KeyId").value.trim(),
                    key: document.getElementById("storageBuilderR2Key").value.trim()
                };
            default:
                return {};
        }
    }

    function areAllStorageFieldsBlank(provider, fields) {
        switch (provider) {
            case "azure":
                return !fields.accountName && !fields.accountKey;
            case "s3":
                return !fields.bucket && !fields.region && !fields.keyId && !fields.key;
            case "r2":
                return !fields.bucket && !fields.accountId && !fields.keyId && !fields.key;
            default:
                return true;
        }
    }

    function validateStorageProviderFields(provider, fields) {
        const errors = [];

        switch (provider) {
            case "azure":
                if (!fields.accountName || !fields.accountKey) {
                    errors.push("Account Name and Account Key or AccessToken are required for Azure Blob Storage.");
                }

                break;
            case "s3":
                if (!fields.bucket || !fields.region || !fields.keyId || !fields.key) {
                    errors.push("Bucket, Region, KeyId, and Key are required for Amazon S3.");
                }

                break;
            case "r2":
                if (!fields.bucket || !fields.accountId || !fields.keyId || !fields.key) {
                    errors.push("Bucket, AccountId, KeyId, and Key are required for Cloudflare R2.");
                }

                break;
            default:
                break;
        }

        return errors;
    }

    function buildStorageConnectionString(provider, fields, extraSegments) {
        const segments = [];

        switch (provider) {
            case "azure":
                segments.push("DefaultEndpointsProtocol=" + (fields.protocol || "https"));
                segments.push("AccountName=" + fields.accountName);
                segments.push("AccountKey=" + fields.accountKey);
                segments.push("EndpointSuffix=" + (fields.endpointSuffix || "core.windows.net"));
                break;
            case "s3":
                segments.push("Bucket=" + fields.bucket);
                segments.push("Region=" + fields.region);
                segments.push("KeyId=" + fields.keyId);
                segments.push("Key=" + fields.key);
                break;
            case "r2":
                segments.push("Bucket=" + fields.bucket);
                segments.push("AccountId=" + fields.accountId);
                segments.push("KeyId=" + fields.keyId);
                segments.push("Key=" + fields.key);
                break;
            default:
                break;
        }

        return segments.concat(extraSegments || []).join(";") + ";";
    }

    // Export a small surface for developer diagnostics and Jest unit tests.
    const api = {
        initializeDatabaseConnectionBuilder,
        detectProvider,
        parseConnectionString,
        buildConnectionString,
        validateProviderFields,
        initializeStorageConnectionBuilder,
        detectStorageProvider,
        parseStorageConnectionString,
        buildStorageConnectionString,
        validateStorageProviderFields
    };

    if (typeof window !== "undefined") {
        window.SkyDbConnectionBuilder = api;
    }

    if (typeof module !== "undefined" && module.exports) {
        module.exports = api;
    }

    function initializeConnectionBuilders() {
        initializeDatabaseConnectionBuilder();
        initializeStorageConnectionBuilder();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initializeConnectionBuilders);
    }
    else {
        initializeConnectionBuilders();
    }
}());
