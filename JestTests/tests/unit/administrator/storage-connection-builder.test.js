const path = require('path');
const { describe, test, expect, beforeEach } = require('@jest/globals');

function getScriptModule() {
  jest.resetModules();
  delete window.SkyDbConnectionBuilder;
  return require(path.join(__dirname, '../../../../Sky.MultiTenant-Adminstrator/wwwroot/js/site.js'));
}

function buildStorageDom() {
  document.body.innerHTML = `
    <input id="StorageConn" value="" />
    <button type="button" data-storage-connection-builder data-target-input="StorageConn">Build Storage Connection</button>
    <div class="modal fade" id="storageConnectionBuilderModal" tabindex="-1" aria-labelledby="storageConnectionBuilderModalLabel" aria-hidden="true">
      <div class="modal-dialog modal-lg modal-dialog-scrollable">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title" id="storageConnectionBuilderModalLabel">Storage Connection Builder</h5>
          </div>
          <div class="modal-body">
            <div class="alert alert-danger d-none" data-storage-builder-validation role="alert"></div>
            <div class="alert alert-warning d-none" data-storage-builder-unsupported role="alert"></div>
            <div data-storage-builder-provider-selection>
              <button type="button" data-storage-provider-option="azure">Azure Blob Storage</button>
              <button type="button" data-storage-provider-option="s3">Amazon S3</button>
              <button type="button" data-storage-provider-option="r2">Cloudflare R2</button>
            </div>
            <div class="d-none" data-storage-builder-provider-forms>
              <span data-storage-builder-provider-label></span>
              <div class="d-none" data-storage-provider-form="azure">
                <input id="storageBuilderAzureProtocol" value="https" />
                <input id="storageBuilderAzureAccountName" />
                <input id="storageBuilderAzureEndpointSuffix" value="core.windows.net" />
                <input id="storageBuilderAzureAccountKey" />
              </div>
              <div class="d-none" data-storage-provider-form="s3">
                <input id="storageBuilderS3Bucket" />
                <input id="storageBuilderS3Region" />
                <input id="storageBuilderS3KeyId" />
                <input id="storageBuilderS3Key" />
              </div>
              <div class="d-none" data-storage-provider-form="r2">
                <input id="storageBuilderR2Bucket" />
                <input id="storageBuilderR2AccountId" />
                <input id="storageBuilderR2KeyId" />
                <input id="storageBuilderR2Key" />
              </div>
            </div>
          </div>
          <div class="modal-footer">
            <button type="button" data-storage-builder-clear>Clear</button>
            <button type="button" data-storage-builder-save>Save</button>
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

describe('SkyDbConnectionBuilder storage helpers', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    global.confirm = jest.fn(() => true);
    window.bootstrap = undefined;
  });

  test('detectStorageProvider identifies Azure, S3, and Cloudflare R2', () => {
    const module = getScriptModule();

    expect(module.detectStorageProvider('DefaultEndpointsProtocol=https;AccountName=acct;AccountKey=key;EndpointSuffix=core.windows.net;')).toBe('azure');
    expect(module.detectStorageProvider('Bucket=my-bucket;Region=us-west-2;KeyId=abc;Key=def;')).toBe('s3');
    expect(module.detectStorageProvider('Bucket=my-bucket;AccountId=acct123;KeyId=abc;Key=def;')).toBe('r2');
  });

  test('parseStorageConnectionString extracts Azure fields and preserves extras', () => {
    const module = getScriptModule();
    const parsed = module.parseStorageConnectionString(
      'DefaultEndpointsProtocol=https;AccountName=acct;AccountKey=AccessToken;EndpointSuffix=core.windows.net;BlobEndpoint=https://acct.blob.core.windows.net;',
      'azure');

    expect(parsed.fields).toEqual({
      protocol: 'https',
      accountName: 'acct',
      accountKey: 'AccessToken',
      endpointSuffix: 'core.windows.net'
    });
    expect(parsed.extraSegments).toEqual(['BlobEndpoint=https://acct.blob.core.windows.net']);
  });

  test('buildStorageConnectionString creates normalized S3 connection strings', () => {
    const module = getScriptModule();
    const connectionString = module.buildStorageConnectionString(
      's3',
      {
        bucket: 'my-bucket',
        region: 'us-west-2',
        keyId: 'abc',
        key: 'def'
      },
      ['Custom=true']);

    expect(connectionString).toBe('Bucket=my-bucket;Region=us-west-2;KeyId=abc;Key=def;Custom=true;');
  });

  test('validateStorageProviderFields applies required field checks', () => {
    const module = getScriptModule();

    expect(module.validateStorageProviderFields('r2', {
      bucket: 'my-bucket',
      accountId: '',
      keyId: 'abc',
      key: 'def'
    })).toEqual([
      'Bucket, AccountId, KeyId, and Key are required for Cloudflare R2.'
    ]);

    expect(module.validateStorageProviderFields('s3', {
      bucket: 'my-bucket',
      region: 'us-east-1',
      keyId: 'abc',
      key: 'def'
    })).toEqual([]);
  });

  test('saving an R2 form writes the built storage connection string back to StorageConn', () => {
    buildStorageDom();
    const modal = createBootstrapStub();

    getScriptModule();

    document.querySelector('[data-storage-connection-builder]').click();
    document.querySelector('[data-storage-provider-option="r2"]').click();

    document.getElementById('storageBuilderR2Bucket').value = 'my-bucket';
    document.getElementById('storageBuilderR2AccountId').value = 'acct123';
    document.getElementById('storageBuilderR2KeyId').value = 'abc';
    document.getElementById('storageBuilderR2Key').value = 'def';

    document.querySelector('[data-storage-builder-save]').click();

    expect(document.getElementById('StorageConn').value).toBe('Bucket=my-bucket;AccountId=acct123;KeyId=abc;Key=def;');
    expect(modal.hide).toHaveBeenCalled();
  });

  test('clear followed by save confirms and removes existing storage connection string', () => {
    buildStorageDom();
    createBootstrapStub();
    const storageConn = document.getElementById('StorageConn');
    storageConn.value = 'Bucket=my-bucket;Region=us-west-2;KeyId=abc;Key=def;';

    getScriptModule();

    document.querySelector('[data-storage-connection-builder]').click();
    document.querySelector('[data-storage-builder-clear]').click();
    document.querySelector('[data-storage-builder-save]').click();

    expect(global.confirm).toHaveBeenCalled();
    expect(storageConn.value).toBe('');
  });
});
