/**
 * RORSH Cryptographic Utilities
 * Provides AES-256-GCM encryption, key derivation, and RorshKey generation
 */

const crypto = require('crypto');

const ALGORITHM = 'aes-256-gcm';
const IV_LENGTH = 16;
const TAG_LENGTH = 16;
const SALT_LENGTH = 32;
const KEY_LENGTH = 32;
const ITERATIONS = 100000;

/**
 * Generate a random 10-digit RorshKey
 * Format: 10 numeric digits
 */
function generateRorshKey() {
    let key = '';
    for (let i = 0; i < 10; i++) {
        key += Math.floor(Math.random() * 10);
    }
    return key;
}

/**
 * Derive AES-256 key from a shared secret using PBKDF2
 */
function deriveKey(sharedSecret, salt = null) {
    if (!salt) {
        salt = crypto.randomBytes(SALT_LENGTH);
    } else if (typeof salt === 'string') {
        salt = Buffer.from(salt, 'hex');
    }

    const key = crypto.pbkdf2Sync(sharedSecret, salt, ITERATIONS, KEY_LENGTH, 'sha256');
    return {
        key: key.toString('hex'),
        salt: salt.toString('hex')
    };
}

/**
 * Encrypt payload using AES-256-GCM
 * Returns: iv + authTag + ciphertext (all hex encoded)
 */
function encryptPayload(plaintext, keyData) {
    try {
        const key = typeof keyData === 'string' ? Buffer.from(keyData, 'hex') : keyData.key ? Buffer.from(keyData.key, 'hex') : keyData;
        const iv = crypto.randomBytes(IV_LENGTH);
        const cipher = crypto.createCipheriv(ALGORITHM, key, iv);

        let encrypted = cipher.update(plaintext, 'utf8', 'hex');
        encrypted += cipher.final('hex');

        const tag = cipher.getAuthTag();

        // Format: iv:tag:ciphertext
        return iv.toString('hex') + ':' + tag.toString('hex') + ':' + encrypted;
    } catch (err) {
        console.error('Encryption error:', err.message);
        return null;
    }
}

/**
 * Decrypt payload using AES-256-GCM
 * Input: iv + authTag + ciphertext (hex encoded, colon separated)
 */
function decryptPayload(encryptedData, keyData) {
    try {
        const key = typeof keyData === 'string' ? Buffer.from(keyData, 'hex') : keyData.key ? Buffer.from(keyData.key, 'hex') : keyData;
        const parts = encryptedData.split(':');

        if (parts.length !== 3) {
            throw new Error('Invalid encrypted data format');
        }

        const iv = Buffer.from(parts[0], 'hex');
        const tag = Buffer.from(parts[1], 'hex');
        const ciphertext = parts[2];

        const decipher = crypto.createDecipheriv(ALGORITHM, key, iv);
        decipher.setAuthTag(tag);

        let decrypted = decipher.update(ciphertext, 'hex', 'utf8');
        decrypted += decipher.final('utf8');

        return decrypted;
    } catch (err) {
        console.error('Decryption error:', err.message);
        return null;
    }
}

/**
 * Generate ECDH key pair for session key exchange
 */
function generateKeyPair() {
    const ecdh = crypto.createECDH('secp256k1');
    ecdh.generateKeys();
    return {
        privateKey: ecdh.getPrivateKey('hex'),
        publicKey: ecdh.getPublicKey('hex')
    };
}

/**
 * Compute shared secret from private key and remote public key
 */
function computeShared(privateKeyHex, publicKeyHex) {
    const ecdh = crypto.createECDH('secp256k1');
    ecdh.setPrivateKey(privateKeyHex, 'hex');
    return ecdh.computeSecret(publicKeyHex, 'hex');
}

/**
 * Hash data using SHA-256
 */
function sha256(data) {
    return crypto.createHash('sha256').update(data).digest('hex');
}

module.exports = {
    generateRorshKey,
    deriveKey,
    encryptPayload,
    decryptPayload,
    generateKeyPair,
    computeShared,
    sha256
};
