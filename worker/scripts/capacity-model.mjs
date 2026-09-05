const profiles = [1_000, 10_000, 100_000];
const days = 30;
const revocationChecksPerClientDay = 4;
const catalogSyncsPerPairDay = 1;
const requestsPerCatalogSync = 10;
const invitationsPerClientMonth = 1;
const requestsPerInvitation = 8;
const averageCiphertextBytes = 512 * 1024;

const normalDailyCeiling = 25_000;
const safetyDailyCeiling = 10_000;

console.log("clients,requests/month,normal-requests/day,safety-requests/day,catalog-upload-gib/month,peak-temporary-storage-gib,fits-app-ceilings");
for (const clients of profiles) {
  const pairs = Math.ceil(clients / 2);
  const revocationRequests = clients * revocationChecksPerClientDay * days;
  const catalogSyncs = pairs * catalogSyncsPerPairDay * days;
  const catalogRequests = catalogSyncs * requestsPerCatalogSync;
  const invitationRequests = clients * invitationsPerClientMonth * requestsPerInvitation;
  const totalRequests = revocationRequests + catalogRequests + invitationRequests;
  const normalRequestsPerDay = (catalogRequests + invitationRequests) / days;
  const safetyRequestsPerDay = revocationRequests / days;
  const uploadGib = catalogSyncs * averageCiphertextBytes / 1024 ** 3;
  const peakGib = catalogSyncs / days / 24 * averageCiphertextBytes / 1024 ** 3;
  const fits = normalRequestsPerDay <= normalDailyCeiling && safetyRequestsPerDay <= safetyDailyCeiling;
  console.log(`${clients},${totalRequests},${Math.ceil(normalRequestsPerDay)},${Math.ceil(safetyRequestsPerDay)},${uploadGib.toFixed(2)},${peakGib.toFixed(3)},${fits}`);
}
