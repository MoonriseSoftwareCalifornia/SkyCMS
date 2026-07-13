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

    // State for the currently open modal session. This is reset whenever the modal closes.
    const state = {
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

    // Export a small surface for developer diagnostics and Jest unit tests.
    const api = {
        initializeDatabaseConnectionBuilder,
        detectProvider,
        parseConnectionString,
        buildConnectionString,
        validateProviderFields
    };

    if (typeof window !== "undefined") {
        window.SkyDbConnectionBuilder = api;
    }

    if (typeof module !== "undefined" && module.exports) {
        module.exports = api;
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initializeDatabaseConnectionBuilder);
    }
    else {
        initializeDatabaseConnectionBuilder();
    }
}());
