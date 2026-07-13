const path = require('path');
const { describe, test, expect, beforeEach } = require('@jest/globals');

function getScriptModule() {
  jest.resetModules();
  delete window.SkyDbConnectionBuilder;
  return require(path.join(__dirname, '../../../../Sky.MultiTenant-Adminstrator/wwwroot/js/site.js'));
}

function buildDom() {
  document.body.innerHTML = `
    <input id="DbConn" value="" />
    <button type="button" data-db-connection-builder data-target-input="DbConn">Build Connection String</button>
    <div class="modal fade" id="dbConnectionBuilderModal" tabindex="-1" aria-labelledby="dbConnectionBuilderModalLabel" aria-hidden="true">
      <div class="modal-dialog modal-lg modal-dialog-scrollable">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title" id="dbConnectionBuilderModalLabel">Database Connection Builder</h5>
            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
          </div>
          <div class="modal-body">
            <div class="alert alert-danger d-none" data-db-builder-validation role="alert"></div>
            <div class="alert alert-warning d-none" data-db-builder-unsupported role="alert"></div>
            <div data-db-builder-provider-selection>
              <button type="button" data-provider-option="cosmos">Azure Cosmos DB</button>
              <button type="button" data-provider-option="sqlserver">SQL Server</button>
              <button type="button" data-provider-option="mysql">MySQL</button>
              <button type="button" data-provider-option="sqlite">SQLite</button>
            </div>
            <div class="d-none" data-db-builder-provider-forms>
              <div class="mb-3">
                <span class="badge text-bg-secondary" data-db-builder-provider-label></span>
              </div>
              <div class="d-none" data-provider-form="cosmos">
                <input type="text" id="dbBuilderCosmosAccountEndpoint" data-field="accountEndpoint" />
                <input type="text" id="dbBuilderCosmosDatabase" data-field="database" />
                <input type="text" id="dbBuilderCosmosAccountKey" data-field="accountKey" />
              </div>
              <div class="d-none" data-provider-form="sqlserver">
                <input type="text" id="dbBuilderSqlServerServer" data-field="server" />
                <input type="text" id="dbBuilderSqlServerDatabase" data-field="database" />
                <select id="dbBuilderSqlServerAuthentication" data-field="authentication">
                  <option value="Sql">SQL Server Authentication</option>
                  <option value="Integrated">Integrated Security</option>
                </select>
                <div data-sql-auth-fields>
                  <input type="text" id="dbBuilderSqlServerUserId" data-field="userId" />
                </div>
                <div data-sql-auth-fields>
                  <input type="text" id="dbBuilderSqlServerPassword" data-field="password" />
                </div>
              </div>
              <div class="d-none" data-provider-form="mysql">
                <input type="text" id="dbBuilderMySqlServer" data-field="server" />
                <input type="text" id="dbBuilderMySqlPort" data-field="port" value="3306" />
                <input type="text" id="dbBuilderMySqlDatabase" data-field="database" />
                <input type="text" id="dbBuilderMySqlUserId" data-field="userId" />
                <input type="text" id="dbBuilderMySqlPassword" data-field="password" />
              </div>
              <div class="d-none" data-provider-form="sqlite">
                <input type="text" id="dbBuilderSqliteDataSource" data-field="dataSource" />
                <input type="text" id="dbBuilderSqlitePassword" data-field="password" />
              </div>
            </div>
          </div>
          <div class="modal-footer">
            <button type="button" data-db-builder-clear>Clear</button>
            <button type="button" data-db-builder-save>Save</button>
          </div>
        </div>
      </div>
    </div>`;
}

function createBootstrapStub() {
  const modal = {
    show: jest.fn(),
    hide: jest.fn()
  };

  window.bootstrap = {
    Modal: {
      getOrCreateInstance: jest.fn(() => modal)
    }
  };

  return modal;
}

describe('SkyDbConnectionBuilder', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    global.confirm = jest.fn(() => true);
    window.bootstrap = undefined;
  });

  test('detectProvider identifies the supported provider formats', () => {
    const module = getScriptModule();

    expect(module.detectProvider('AccountEndpoint=https://acct.documents.azure.com:443/;AccountKey=key;Database=skycms;')).toBe('cosmos');
    expect(module.detectProvider('Server=db.example;Port=3307;uid=user;pwd=secret;database=skycms;')).toBe('mysql');
    expect(module.detectProvider('Server=db.example;Initial Catalog=skycms;User ID=sa;Password=secret;')).toBe('sqlserver');
    expect(module.detectProvider('Data Source=app.db;')).toBe('sqlite');
  });

  test('parseConnectionString extracts MySQL fields and preserves extra segments', () => {
    const module = getScriptModule();
    const parsed = module.parseConnectionString(
      'Server=db.example;Port=3307;uid=user;pwd=secret;database=skycms;SslMode=Required;',
      'mysql');

    expect(parsed.fields).toEqual({
      server: 'db.example',
      port: '3307',
      database: 'skycms',
      userId: 'user',
      password: 'secret'
    });
    expect(parsed.extraSegments).toEqual(['SslMode=Required']);
  });

  test('buildConnectionString creates SQL Server integrated security strings and appends extra segments', () => {
    const module = getScriptModule();
    const connectionString = module.buildConnectionString(
      'sqlserver',
      {
        server: 'sql.example',
        database: 'skycms',
        authentication: 'Integrated',
        userId: '',
        password: ''
      },
      ['Encrypt=True']);

    expect(connectionString).toBe('Server=sql.example;Initial Catalog=skycms;Integrated Security=True;Encrypt=True;');
  });

  test('validateProviderFields reports provider-specific required field errors', () => {
    const module = getScriptModule();

    expect(module.validateProviderFields('cosmos', {
      accountEndpoint: 'https://acct.documents.azure.com:443/',
      database: 'skycms',
      accountKey: ''
    })).toEqual([
      'Account Endpoint, Database, and Account Key or AccessToken are required for Azure Cosmos DB.'
    ]);

    expect(module.validateProviderFields('sqlite', {
      dataSource: 'app.db',
      password: ''
    })).toEqual([]);
  });

  test('opening with an existing connection string populates the matching provider form', () => {
    buildDom();
    const modal = createBootstrapStub();
    const dbConn = document.getElementById('DbConn');
    dbConn.value = 'Server=db.example;Port=3307;uid=user;pwd=secret;database=skycms;';

    getScriptModule();

    document.querySelector('[data-db-connection-builder]').click();

    expect(modal.show).toHaveBeenCalled();
    expect(document.querySelector('[data-provider-form="mysql"]').classList.contains('d-none')).toBe(false);
    expect(document.getElementById('dbBuilderMySqlServer').value).toBe('db.example');
    expect(document.getElementById('dbBuilderMySqlPort').value).toBe('3307');
    expect(document.getElementById('dbBuilderMySqlUserId').value).toBe('user');
  });

  test('saving a SQLite form writes the built connection string back to DbConn', () => {
    buildDom();
    const modal = createBootstrapStub();

    getScriptModule();

    document.querySelector('[data-db-connection-builder]').click();
    document.querySelector('[data-provider-option="sqlite"]').click();
    document.getElementById('dbBuilderSqliteDataSource').value = 'app.db';
    document.getElementById('dbBuilderSqlitePassword').value = 'secret';

    document.querySelector('[data-db-builder-save]').click();

    expect(document.getElementById('DbConn').value).toBe('Data Source=app.db;Password=secret;');
    expect(modal.hide).toHaveBeenCalled();
  });

  test('clear followed by save confirms and removes the existing connection string', () => {
    buildDom();
    createBootstrapStub();
    const dbConn = document.getElementById('DbConn');
    dbConn.value = 'Server=db.example;Initial Catalog=skycms;User ID=sa;Password=secret;';

    getScriptModule();

    document.querySelector('[data-db-connection-builder]').click();
    document.querySelector('[data-db-builder-clear]').click();

    expect(document.querySelector('[data-db-builder-provider-selection]').classList.contains('d-none')).toBe(false);

    document.querySelector('[data-db-builder-save]').click();

    expect(global.confirm).toHaveBeenCalled();
    expect(dbConn.value).toBe('');
  });
});
