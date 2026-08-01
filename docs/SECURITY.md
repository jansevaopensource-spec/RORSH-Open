# Security Guide

## Encryption

RORSH uses AES-256-GCM for all message encryption.

### Key Derivation
- Server: 32-byte hex key from ENCRYPTION_KEY env variable
- Admin: SHA-256 of (password + salt)
- Client: SHA-256 of static salt (must match server)

### Message Format
```
[IV (12 bytes)] + [Ciphertext + AuthTag]
```

### Important Notes
- Ensure ENCRYPTION_KEY is kept secret
- Use strong admin passwords
- The salt value should be changed in production

## Network Security
- WSS (WebSocket Secure) prevents MITM attacks
- All payloads are encrypted end-to-end
- Server acts as relay, cannot read command content

## Operational Security
- RCS runs with current user privileges
- No privilege escalation built-in
- Commands execute in user context only

## Recommendations
1. Use strong, unique ENCRYPTION_KEY
2. Change default salt before deployment
3. Restrict server access with firewall rules
4. Monitor connection logs
5. Rotate admin credentials regularly
