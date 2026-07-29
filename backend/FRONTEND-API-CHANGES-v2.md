# Frontend API Integration Changes v2
**Date:** July 2026
**Scope:** Vulnerability Fixes, Clean Architecture Refactoring, & Wallet Concurrency Control

This document outlines the recent business logic and API changes that impact the Frontend integration. Please review these updates to ensure proper UI handling and data alignment.

---

## 1. Withdrawal Flow Update (Escrow Approach)
**Endpoint:** `POST /api/financials/wallet/withdraw`
**What Changed:**
Withdrawing funds now **immediately deducts** the requested amount from the user's `Balance` while placing the withdrawal transaction in a "Pending" state.
- **Why:** This prevents the user from concurrently spending the pending funds on bets (arbitrage vulnerability). If the admin rejects the withdrawal, the backend will automatically refund the amount back to the user's balance.
**UI Impact:**
- The frontend should reflect the deducted balance immediately upon a successful withdrawal request.
- The UI should display the "Pending" withdrawal transaction in the transaction history so the user knows their funds are reserved but not yet paid out.

## 2. Wallet Concurrency Control
**Endpoints:** `POST /api/financials/wallet/withdraw`, `POST /api/financials/wallet/deposit`, `POST /api/betting/place`
**What Changed:**
We introduced EF Core `RowVersion` concurrency checks to prevent race conditions (e.g., users rapidly clicking the deposit/withdraw/bet button).
- If a concurrent modification is detected, the API will fail fast and return a `400 Bad Request` with the message: _"Your wallet balance was modified by another transaction. Please try again."_
**UI Impact:**
- **Critical:** Please ensure that action buttons (Deposit, Withdraw, Place Bet) are **disabled and show a loading spinner** while the API request is in progress to prevent accidental spamming.
- The UI should gracefully catch this `400 Bad Request` and display the error message to the user, allowing them to manually retry.

## 3. Betting Rules Enforcement
**Endpoint:** `POST /api/betting/place`
**What Changed:**
- **Single Horse Rule:** A user can no longer place bets on multiple different horses in the exact same race. Attempting to do so will return a `400 Bad Request` (e.g., _"You have already placed a bet on another horse in this race."_).
- **Minimum Bet Limit:** A strict minimum bet amount (e.g., $1.00) is now enforced by the backend to prevent micro-bet spam.

## 4. Admin User Locking (Validation Blockers)
**Endpoint:** `PUT /api/admin/users/{userId}/status` (Lock/Unlock)
**What Changed:**
Admins can no longer immediately lock a user who has active dependencies (e.g., an active Jockey contract, a pending bet, or an active Referee assignment).
- If blockers exist, the API will return a `400 Bad Request` containing a `blockers` array detailing the specific reasons the user cannot be locked.
**UI Impact:**
- The admin dashboard UI needs to catch this `400` error and map/render the `blockers` string array in a list or modal, explaining to the Admin exactly why the action was blocked (e.g., "User has an active bet in Race 5").

## 5. Tournament Registration & Health Validation
**Endpoint:** `POST /api/tournaments/register` (or similar registration endpoint)
**What Changed:**
- **Health Status:** The API now actively blocks registering a horse if its `HealthStatus` is marked as `'Injured'` or `'Recovering'`.
- **Date Overlap:** The system checks for scheduling conflicts. A horse cannot be registered in a tournament if it is already registered in another tournament whose start and end dates overlap with the new one.
**UI Impact:**
- Ensure the UI conveys these specific validation errors to the Manager/Admin registering the horse.

## 6. Medical Check Errors
**Endpoint:** `POST /api/medical-checks`
**What Changed:**
Instead of returning a single generic error string when a horse fails a medical check, the API now compiles a detailed list of all failing conditions (Temperature, Heart Rate, Weight anomalies, or Doping).
- The response will return a combined, structured list/string of these errors.
**UI Impact:**
- The UI should parse and display these detailed bullet points to the Veterinarian so they know exactly which vital signs caused the rejection.

---
_Please reach out to the backend team if any endpoint payloads or response DTO structures require further alignment!_
