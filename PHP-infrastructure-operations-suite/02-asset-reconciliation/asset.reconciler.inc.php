<?php
/* Scan workflow and expected-versus-observed reconciliation. */

require_once(__DIR__ . '/asset.auditor.inc.php');

class AssetReconciler
{
	var $Auditor;

	function __construct()
	{
		$this->Auditor = new AssetAuditor();
	}

	function StartAudit($SiteCode, $Types, $UserId, $ReAuditOf = 0)
	{
		$Viewer = new AssetViewer($SiteCode, $Types);
		return $this->Auditor->CreateAudit($SiteCode, $Types, $UserId, $Viewer->GetExpectedAssets(), $ReAuditOf);
	}

	function CompleteAudit($AuditId, $UserId, $Notes)
	{
		return $this->Auditor->CompleteAudit($AuditId, $UserId, $Notes);
	}

	function RecordScan($AuditId, $Input, $UserId)
	{
		$AuditId = (int)$AuditId;
		$Input = trim((string)$Input);
		$Meta = $this->Auditor->GetAudit($AuditId);
		if (!$Meta) {
			return ['ERROR' => 'Audit not found.'];
		}
		if ($Meta['COMPLETED'] === 'Y') {
			return ['ERROR' => 'Completed audits cannot be changed.'];
		}
		if ($Input === '') {
			return ['ERROR' => 'Asset ID or serial is required.'];
		}

		$Viewer = new AssetViewer($Meta['SITE_CODE'], $Meta['TYPES_ARRAY']);
		$Asset = $Viewer->FindAsset($Input);
		$Scan = $this->BuildScan($Meta, $Input, $Asset, $UserId);
		$this->Auditor->SetScan($AuditId, $Scan['SCAN_KEY'], $Scan);
		return $Scan;
	}

	function RecordScansBatch($AuditId, $Inputs, $UserId)
	{
		$AuditId = (int)$AuditId;
		$Meta = $this->Auditor->GetAudit($AuditId);
		if (!$Meta || $Meta['COMPLETED'] === 'Y') {
			return ['ERROR' => $Meta ? 'Completed audits cannot be changed.' : 'Audit not found.'];
		}

		$Inputs = array_values(array_unique(array_filter(array_map('trim', (array)$Inputs))));
		$Viewer = new AssetViewer($Meta['SITE_CODE'], $Meta['TYPES_ARRAY']);
		$Lookup = $Viewer->FindAssets($Inputs);
		$Scans = [];
		foreach ($Inputs as $Input) {
			$Asset = $Lookup[DemoAssetRepository::Normalize($Input)] ?? [];
			$Scan = $this->BuildScan($Meta, $Input, $Asset, $UserId);
			$this->Auditor->SetScan($AuditId, $Scan['SCAN_KEY'], $Scan);
			$Scans[] = $Scan;
		}

		return ['SCANS' => $Scans];
	}

	function BuildScan($Meta, $Input, $Asset, $UserId)
	{
		$Status = 'NOT_FOUND';
		if ($Asset) {
			$Status = $Asset['SITE_CODE'] === $Meta['SITE_CODE'] ? 'PRESENT' : 'ELSEWHERE';
			if (($Asset['PENDING_FLAG'] ?? 'N') === 'Y') {
				$Status = 'PROBLEM';
			}
		}

		$AssetId = $Asset['ASSET_ID'] ?? '';
		$Serial = $Asset['SERIAL'] ?? $Input;
		$Key = DemoAssetRepository::Normalize($AssetId ?: $Serial ?: $Input);
		return [
			'SCAN_KEY' => $Key,
			'INPUT' => $Input,
			'ASSET_ID' => $AssetId,
			'SERIAL' => $Serial,
			'MODEL' => $Asset['MODEL'] ?? '',
			'SITE_CODE' => $Asset['SITE_CODE'] ?? '',
			'TYPE' => $Asset['TYPE'] ?? '',
			'STATUS' => $Status,
			'STATUS_DETAIL' => $Status === 'PROBLEM' ? 'PENDING_REVIEW' : ($Status === 'ELSEWHERE' ? $Asset['SITE_CODE'] : ''),
			'SCANNED_BY' => $UserId,
			'SCANNED_DATE' => date('Y-m-d H:i:s')
		];
	}

	function GetAuditState($AuditId)
	{
		$Meta = $this->Auditor->GetAudit($AuditId);
		if (!$Meta) {
			return [];
		}

		$Scans = $this->Auditor->GetScans($AuditId);
		$ScanMap = [];
		foreach ($Scans as $Scan) {
			foreach ([$Scan['ASSET_ID'], $Scan['SERIAL']] as $Value) {
				$Key = DemoAssetRepository::Normalize($Value);
				if ($Key !== '') {
					$ScanMap[$Key] = $Scan;
				}
			}
		}

		$Expected = [];
		foreach ($this->Auditor->GetExpected($AuditId) as $Asset) {
			$Match = $ScanMap[DemoAssetRepository::Normalize($Asset['ASSET_ID'])]
				?? $ScanMap[DemoAssetRepository::Normalize($Asset['SERIAL'])]
				?? [];
			$Asset['RECONCILIATION_STATUS'] = $Match && $Match['STATUS'] === 'PRESENT' ? 'PRESENT' : 'MISSING';
			$Expected[] = $Asset;
		}

		return [
			'META' => $Meta,
			'EXPECTED' => $Expected,
			'SCANNED' => array_reverse($Scans),
			'SUMMARY' => $this->BuildSummary($Expected, $Scans)
		];
	}

	function BuildSummary($Expected, $Scans)
	{
		$Summary = ['EXPECTED' => count($Expected), 'PRESENT' => 0, 'MISSING' => 0, 'ELSEWHERE' => 0, 'NOT_FOUND' => 0, 'PROBLEM' => 0];
		foreach ($Expected as $Asset) {
			$Summary[$Asset['RECONCILIATION_STATUS']]++;
		}
		foreach ($Scans as $Scan) {
			if (isset($Summary[$Scan['STATUS']]) && $Scan['STATUS'] !== 'PRESENT') {
				$Summary[$Scan['STATUS']]++;
			}
		}
		return $Summary;
	}

	function RemoveScan($AuditId, $Key)
	{
		$Meta = $this->Auditor->GetAudit($AuditId);
		if (!$Meta || $Meta['COMPLETED'] === 'Y') {
			return $Meta ? 'Completed audits cannot be changed.' : 'Audit not found.';
		}
		$this->Auditor->RemoveScan($AuditId, DemoAssetRepository::Normalize($Key));
		return true;
	}

	function ClearScans($AuditId)
	{
		$Meta = $this->Auditor->GetAudit($AuditId);
		if (!$Meta || $Meta['COMPLETED'] === 'Y') {
			return $Meta ? 'Completed audits cannot be changed.' : 'Audit not found.';
		}
		$this->Auditor->ClearScans($AuditId);
		return true;
	}

	function ReAudit($AuditId, $UserId)
	{
		$OldMeta = $this->Auditor->GetAudit($AuditId);
		if (!$OldMeta || $OldMeta['COMPLETED'] !== 'Y') {
			return 'Only completed audits can be repeated.';
		}

		$NewMeta = $this->StartAudit($OldMeta['SITE_CODE'], $OldMeta['TYPES_ARRAY'], $UserId, $AuditId);
		if (!is_array($NewMeta)) {
			return $NewMeta;
		}

		$Inputs = array_map(function ($Scan) {
			return $Scan['ASSET_ID'] ?: $Scan['SERIAL'];
		}, $this->Auditor->GetScans($AuditId));
		$this->RecordScansBatch($NewMeta['AUDIT_ID'], $Inputs, $UserId);
		return $this->GetAuditState($NewMeta['AUDIT_ID']);
	}
}

?>
