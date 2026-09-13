<?php
/* Audit lifecycle and snapshot storage. */

require_once(__DIR__ . '/asset.viewer.inc.php');

class AssetAuditor
{
	function __construct()
	{
		if (!isset($_SESSION['PORTFOLIO_ASSET_AUDITS'])) {
			$_SESSION['PORTFOLIO_ASSET_AUDITS'] = [];
			$_SESSION['PORTFOLIO_ASSET_AUDIT_NEXT_ID'] = 1;
		}
	}

	function CreateAudit($SiteCode, $Types, $UserId, $ExpectedAssets, $ReAuditOf = 0)
	{
		$SiteCode = strtoupper(trim((string)$SiteCode));
		$Types = DemoAssetRepository::NormalizeTypes($Types);
		if ($SiteCode === '') {
			return 'Site code is required.';
		}
		if ($this->GetActiveAudit($SiteCode, $Types)) {
			return 'An audit is already in progress for this site and asset selection.';
		}

		$AuditId = (int)$_SESSION['PORTFOLIO_ASSET_AUDIT_NEXT_ID']++;
		$Now = date('Y-m-d H:i:s');
		$_SESSION['PORTFOLIO_ASSET_AUDITS'][$AuditId] = [
			'META' => [
				'AUDIT_ID' => $AuditId,
				'SITE_CODE' => $SiteCode,
				'TYPES' => implode(',', $Types),
				'TYPES_ARRAY' => $Types,
				'CREATED_BY' => $UserId,
				'CREATED_DATE' => $Now,
				'COMPLETED_BY' => '',
				'COMPLETED_DATE' => '',
				'COMPLETION_NOTES' => '',
				'RE_AUDIT_OF' => (int)$ReAuditOf
			],
			'EXPECTED' => array_values($ExpectedAssets),
			'SCANNED' => []
		];

		return $this->GetAudit($AuditId);
	}

	function CompleteAudit($AuditId, $UserId, $Notes)
	{
		$AuditId = (int)$AuditId;
		$Notes = trim((string)$Notes);
		if (!$this->AuditExists($AuditId)) {
			return 'Audit not found.';
		}
		if ($this->IsCompleted($AuditId)) {
			return 'Audit is already completed.';
		}
		if ($Notes === '') {
			return 'Completion notes are required.';
		}

		$_SESSION['PORTFOLIO_ASSET_AUDITS'][$AuditId]['META']['COMPLETED_BY'] = $UserId;
		$_SESSION['PORTFOLIO_ASSET_AUDITS'][$AuditId]['META']['COMPLETED_DATE'] = date('Y-m-d H:i:s');
		$_SESSION['PORTFOLIO_ASSET_AUDITS'][$AuditId]['META']['COMPLETION_NOTES'] = $Notes;
		return true;
	}

	function GetAudit($AuditId)
	{
		$AuditId = (int)$AuditId;
		if (!$this->AuditExists($AuditId)) {
			return [];
		}

		$Meta = $_SESSION['PORTFOLIO_ASSET_AUDITS'][$AuditId]['META'];
		$Meta['COMPLETED'] = $this->IsCompleted($AuditId) ? 'Y' : 'N';
		$Meta['DOCUMENT_STATUS'] = $Meta['COMPLETED'] === 'Y' ? 'COMPLETED' : 'DRAFT';
		$Meta['EXPECTED_COUNT'] = count($this->GetExpected($AuditId));
		$Meta['SCANNED_COUNT'] = count($this->GetScans($AuditId));
		return $Meta;
	}

	function GetAuditList($SiteCode)
	{
		$SiteCode = strtoupper(trim((string)$SiteCode));
		$Audits = [];
		foreach (array_keys($_SESSION['PORTFOLIO_ASSET_AUDITS']) as $AuditId) {
			$Audit = $this->GetAudit($AuditId);
			if ($Audit['SITE_CODE'] === $SiteCode) {
				$Audits[] = $Audit;
			}
		}
		usort($Audits, function ($First, $Second) {
			return $Second['AUDIT_ID'] <=> $First['AUDIT_ID'];
		});
		return $Audits;
	}

	function GetActiveAudit($SiteCode, $Types)
	{
		$TypeKey = implode(',', DemoAssetRepository::NormalizeTypes($Types));
		foreach ($this->GetAuditList($SiteCode) as $Audit) {
			if ($Audit['TYPES'] === $TypeKey && $Audit['COMPLETED'] !== 'Y') {
				return $Audit;
			}
		}

		return [];
	}

	function GetLastCompletedAudit($SiteCode, $Types)
	{
		$TypeKey = implode(',', DemoAssetRepository::NormalizeTypes($Types));
		foreach ($this->GetAuditList($SiteCode) as $Audit) {
			if ($Audit['TYPES'] === $TypeKey && $Audit['COMPLETED'] === 'Y') {
				return $Audit;
			}
		}

		return [];
	}

	function GetExpected($AuditId)
	{
		return $this->AuditExists($AuditId) ? $_SESSION['PORTFOLIO_ASSET_AUDITS'][(int)$AuditId]['EXPECTED'] : [];
	}

	function GetScans($AuditId)
	{
		return $this->AuditExists($AuditId) ? array_values($_SESSION['PORTFOLIO_ASSET_AUDITS'][(int)$AuditId]['SCANNED']) : [];
	}

	function SetScan($AuditId, $Key, $Scan)
	{
		$_SESSION['PORTFOLIO_ASSET_AUDITS'][(int)$AuditId]['SCANNED'][$Key] = $Scan;
	}

	function RemoveScan($AuditId, $Key)
	{
		unset($_SESSION['PORTFOLIO_ASSET_AUDITS'][(int)$AuditId]['SCANNED'][$Key]);
	}

	function ClearScans($AuditId)
	{
		$_SESSION['PORTFOLIO_ASSET_AUDITS'][(int)$AuditId]['SCANNED'] = [];
	}

	function IsCompleted($AuditId)
	{
		$Date = $_SESSION['PORTFOLIO_ASSET_AUDITS'][(int)$AuditId]['META']['COMPLETED_DATE'] ?? '';
		return $Date !== '';
	}

	function AuditExists($AuditId)
	{
		return isset($_SESSION['PORTFOLIO_ASSET_AUDITS'][(int)$AuditId]);
	}
}

?>

