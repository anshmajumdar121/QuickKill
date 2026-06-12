# Security Policy

QuickKill requires **administrator privileges** to kill processes owned by other users or elevated processes.

## Critical process protection

QuickKill explicitly blocks killing these Windows system processes:
- csrss, wininit, winlogon, services, lsass, smss, dwm
- System, Registry, Idle

## Reporting a vulnerability

If you discover a security issue (e.g., a way to bypass the critical process guard), please open an issue with the details. We take these reports seriously.
