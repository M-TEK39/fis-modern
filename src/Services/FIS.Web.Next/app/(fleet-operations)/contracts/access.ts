import type { ContractRecord } from "@/lib/api/finance/api-contracts";

const CONTRACT_PERMISSION = BigInt(2);

const ADMIN_ROLES = [
  "admin",
  "administrator",
  "system administrator",
  "systemadministrator",
] as const;

const CAPTURER_ROLES = [
  "contract (load and manage)",
  "contracts (load and manage)",
  "contract_load_and_manage",
  "contracts_load_and_manage",
  "contract capturer",
  "contracts capturer",
  "contract_capturer",
  "contracts_capturer",
  "capturer",
] as const;

const REVIEWER_ROLES = [
  "contract (approver)",
  "contracts (approver)",
  "contract approver",
  "contracts approver",
  "contract_approver",
  "contracts_approver",
  "back dating contract (approver)",
] as const;

const CANCEL_CLOSE_ROLES = [
  "contract (cancel and close)",
  "contracts (cancel and close)",
  "contract_cancel_and_close",
  "contracts_cancel_and_close",
] as const;

const HISTORY_ROLES = ["contract history back dating", "contract_history_backdating"] as const;

export type ContractSession = Readonly<{
  userAccessCode?: string;
  accessLevel?: string;
  roles: readonly string[];
}>;

function hasNamedRole(roles: readonly string[], candidates: readonly string[]) {
  const candidateSet = new Set(candidates);
  return roles.some((role) => candidateSet.has(role.trim().toLowerCase()));
}

function hasAccessBit(accessLevel: string | undefined, permission: bigint) {
  try {
    return accessLevel ? (BigInt(accessLevel) & permission) === permission : false;
  } catch {
    return false;
  }
}

export function hasContractAccess(accessLevel: string | undefined, roles: readonly string[]) {
  if (hasNamedRole(roles, ["contracts", "contract", ...ADMIN_ROLES])) {
    return true;
  }

  return hasAccessBit(accessLevel, CONTRACT_PERMISSION);
}

export function isContractAdmin(roles: readonly string[]) {
  return hasNamedRole(roles, ADMIN_ROLES);
}

export function hasContractCapturerRole(roles: readonly string[]) {
  return hasNamedRole(roles, CAPTURER_ROLES);
}

export function hasContractReviewerRole(roles: readonly string[]) {
  return hasNamedRole(roles, REVIEWER_ROLES);
}

export function isContractAdministrator(roles: readonly string[]) {
  return isContractAdmin(roles);
}

export function canCaptureNewContract(roles: readonly string[]) {
  return isContractAdministrator(roles) || hasContractCapturerRole(roles);
}

export function canManageActiveContract(roles: readonly string[]) {
  return isContractAdministrator(roles) || hasContractReviewerRole(roles);
}

export function canCloseActiveContract(roles: readonly string[]) {
  return (
    isContractAdministrator(roles) ||
    hasContractCancelAndCloseRole(roles) ||
    hasContractReviewerRole(roles)
  );
}

export function hasContractLoadAndManageRole(roles: readonly string[]) {
  return hasNamedRole(roles, CAPTURER_ROLES);
}

export function hasContractCancelAndCloseRole(roles: readonly string[]) {
  return hasNamedRole(roles, CANCEL_CLOSE_ROLES);
}

export function hasContractHistoryBackdatingRole(roles: readonly string[]) {
  return isContractAdministrator(roles) || hasNamedRole(roles, HISTORY_ROLES);
}

export function getContractOwnerCode(contract: ContractRecord) {
  return contract.createdByUserCode ?? contract.userCode;
}

export function getCurrentUserCode(userAccessCode: string | undefined) {
  const trimmed = userAccessCode?.trim() ?? "";
  if (!trimmed || !/^\d+$/.test(trimmed)) return null;

  const parsed = Number(trimmed);
  return Number.isSafeInteger(parsed) ? parsed : null;
}

export function isContractOwner(contract: ContractRecord, userAccessCode: string | undefined) {
  const currentUserCode = getCurrentUserCode(userAccessCode);
  const ownerCode = getContractOwnerCode(contract);
  return currentUserCode !== null && ownerCode !== null && ownerCode === currentUserCode;
}

export function canEditContract(contract: ContractRecord, session: ContractSession) {
  return (
    isContractAdministrator(session.roles) ||
    (hasContractCapturerRole(session.roles) && isContractOwner(contract, session.userAccessCode))
  );
}

export function canSubmitContract(contract: ContractRecord, session: ContractSession) {
  return (
    isContractAdministrator(session.roles) ||
    (hasContractCapturerRole(session.roles) && isContractOwner(contract, session.userAccessCode))
  );
}

export function canReviewContract(contract: ContractRecord, session: ContractSession) {
  return (
    (isContractAdministrator(session.roles) || hasContractReviewerRole(session.roles)) &&
    !isContractOwner(contract, session.userAccessCode)
  );
}
