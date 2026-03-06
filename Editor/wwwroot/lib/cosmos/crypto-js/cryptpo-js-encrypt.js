/**
 * This file contains functions to encrypt and decrypt data using AES encryption.
 * It allows for runtime key/context bootstrap while preserving legacy fallback behavior.
 */

const ccmsDefaultEncryptionKeyText = "1234567890123456";
let ccmsActiveEncryptionKeyText = ccmsDefaultEncryptionKeyText;
let ccmsEncryptionContextToken = "";

/**
 * setEncryptionKey
 * @param {string} keyText The encryption key.
 * @param {string} contextToken The context token.
 */
function setEncryptionKey(keyText, contextToken) {
    if (typeof keyText === "string" && keyText.length === 16) {
        ccmsActiveEncryptionKeyText = keyText;
    } else {
        ccmsActiveEncryptionKeyText = ccmsDefaultEncryptionKeyText;
    }

    ccmsEncryptionContextToken = contextToken || "";
}

/**
 * getEncryptionContextToken
 * @returns {string} The current encryption context token.
 */
function getEncryptionContextToken() {
    return ccmsEncryptionContextToken;
}

/**
 * decryptWithKey
 * @param {any} encrypedText The text to decrypt.
 * @param {string} keyText The decryption key.
 * @returns decrypted data.
 */
function decryptWithKey(encrypedText, keyText) {
    const key = CryptoJS.enc.Utf8.parse(keyText);

    try {
        const parsed = JSON.parse(encrypedText);
        if (parsed && parsed.iv && parsed.ct) {
            const iv = CryptoJS.enc.Base64.parse(parsed.iv);
            const cipherParams = CryptoJS.lib.CipherParams.create({
                ciphertext: CryptoJS.enc.Base64.parse(parsed.ct)
            });

            const decrypted = CryptoJS.AES.decrypt(cipherParams, key, {
                iv: iv,
                padding: CryptoJS.pad.Pkcs7,
                mode: CryptoJS.mode.CBC
            });

            return decrypted.toString(CryptoJS.enc.Utf8);
        }
    } catch (e) {
    }

    const legacyIv = CryptoJS.enc.Utf8.parse(keyText);
    const decrypted = CryptoJS.AES.decrypt(encrypedText, key, {
        iv: legacyIv,
        padding: CryptoJS.pad.Pkcs7,
        mode: CryptoJS.mode.CBC
    });

    return decrypted.toString(CryptoJS.enc.Utf8);
}

/**
 * encryptData
 * @param {any} plainText The text to encrypt.
 * @returns encrypted data.
 */
function encryptData(plainText) {
    if (typeof plainText === 'undefined' || plainText === null || plainText === "") {
        return "";
    }

    const keyText = ccmsActiveEncryptionKeyText;
    const key = CryptoJS.enc.Utf8.parse(keyText);
    const iv = CryptoJS.lib.WordArray.random(16);

    const encrypted = CryptoJS.AES.encrypt(plainText, key, {
        iv: iv,
        padding: CryptoJS.pad.Pkcs7,
        mode: CryptoJS.mode.CBC
    });

    const payload = {
        v: 2,
        iv: CryptoJS.enc.Base64.stringify(iv),
        ct: encrypted.ciphertext.toString(CryptoJS.enc.Base64)
    };

    return JSON.stringify(payload);
}

/**
 * decryptData
 * @param {any} encrypedText The text to decrypt.
 * @returns decrypted data.
 */
function decryptData(encrypedText) {
    if (typeof encrypedText === 'undefined' || encrypedText === null || encrypedText === "") {
        return "";
    }

    const usingActiveKey = decryptWithKey(encrypedText, ccmsActiveEncryptionKeyText);
    if (usingActiveKey !== "") {
        return usingActiveKey;
    }

    if (ccmsActiveEncryptionKeyText !== ccmsDefaultEncryptionKeyText) {
        return decryptWithKey(encrypedText, ccmsDefaultEncryptionKeyText);
    }

    return usingActiveKey;
}