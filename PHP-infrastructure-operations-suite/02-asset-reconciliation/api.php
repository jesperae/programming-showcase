<?php
require_once(__DIR__ . '/../04-reusable-operations-toolkit/simple.ajax.endpoint.inc.php');
require_once(__DIR__ . '/asset.reconciler.inc.php');

$Request = GetAjaxAction();
$Reconciler = new AssetReconciler();
$UserId = GetDemoUserId();

switch ($Request['ACTION']) {
	case 'GET_CONTEXT':
		$SiteCode = AjaxRequire('site_code');
		$Types = AjaxGet('types', []);
		$Active = $Reconciler->Auditor->GetActiveAudit($SiteCode, $Types);
		$LastCompleted = $Reconciler->Auditor->GetLastCompletedAudit($SiteCode, $Types);
		$Viewer = new AssetViewer($SiteCode, $Types);
		AjaxSuccess([
			'active' => $Active ? $Reconciler->GetAuditState($Active['AUDIT_ID']) : [],
			'last_completed' => $LastCompleted,
			'audits' => $Reconciler->Auditor->GetAuditList($SiteCode),
			'current_inventory' => $Viewer->GetExpectedAssets()
		]);
		break;

	case 'START_AUDIT':
		$Audit = $Reconciler->StartAudit(AjaxRequire('site_code'), AjaxGet('types', []), $UserId);
		if (!is_array($Audit)) {
			AjaxError($Audit, 'START_FAILED');
		}
		AjaxSuccess($Reconciler->GetAuditState($Audit['AUDIT_ID']), 'Started audit ' . $Audit['AUDIT_ID'] . '.');
		break;

	case 'LOAD_AUDIT':
		$State = $Reconciler->GetAuditState((int)AjaxRequire('audit_id'));
		if (!$State) {
			AjaxError('Audit not found.', 'AUDIT_NOT_FOUND', 404);
		}
		AjaxSuccess($State, 'Loaded audit ' . $State['META']['AUDIT_ID'] . '.');
		break;

	case 'CHECK_ASSET':
		$Result = $Reconciler->RecordScan((int)AjaxRequire('audit_id'), AjaxRequire('input'), $UserId);
		if (!empty($Result['ERROR'])) {
			AjaxError($Result['ERROR'], 'SCAN_FAILED');
		}
		AjaxSuccess([
			'scan' => $Result,
			'state' => $Reconciler->GetAuditState((int)AjaxGet('audit_id'))
		], 'Recorded ' . ($Result['ASSET_ID'] ?: $Result['SERIAL']) . '.');
		break;

	case 'MASS_CHECK':
		$AuditId = (int)AjaxRequire('audit_id');
		$Inputs = AjaxGet('inputs', []);
		if (is_string($Inputs)) {
			$Inputs = preg_split('/\r?\n/', $Inputs);
		}
		$Result = $Reconciler->RecordScansBatch($AuditId, $Inputs, $UserId);
		if (!empty($Result['ERROR'])) {
			AjaxError($Result['ERROR'], 'BATCH_SCAN_FAILED');
		}
		AjaxSuccess([
			'processed' => count($Result['SCANS']),
			'state' => $Reconciler->GetAuditState($AuditId)
		], 'Processed ' . count($Result['SCANS']) . ' scan entries.');
		break;

	case 'COMPLETE_AUDIT':
		$AuditId = (int)AjaxRequire('audit_id');
		$Result = $Reconciler->CompleteAudit($AuditId, $UserId, AjaxRequire('notes'));
		if ($Result !== true) {
			AjaxError($Result, 'COMPLETE_FAILED');
		}
		AjaxSuccess($Reconciler->GetAuditState($AuditId), 'Completed audit ' . $AuditId . '.');
		break;

	case 'REMOVE_SCAN':
		$AuditId = (int)AjaxRequire('audit_id');
		$Result = $Reconciler->RemoveScan($AuditId, AjaxRequire('scan_key'));
		if ($Result !== true) {
			AjaxError($Result, 'REMOVE_FAILED');
		}
		AjaxSuccess($Reconciler->GetAuditState($AuditId), 'Removed scan entry.');
		break;

	case 'CLEAR_SCANS':
		$AuditId = (int)AjaxRequire('audit_id');
		$Result = $Reconciler->ClearScans($AuditId);
		if ($Result !== true) {
			AjaxError($Result, 'CLEAR_FAILED');
		}
		AjaxSuccess($Reconciler->GetAuditState($AuditId), 'Cleared scan entries.');
		break;

	case 'RE_AUDIT':
		$State = $Reconciler->ReAudit((int)AjaxRequire('audit_id'), $UserId);
		if (!is_array($State)) {
			AjaxError($State, 'RE_AUDIT_FAILED');
		}
		AjaxSuccess($State, 'Created repeat audit ' . $State['META']['AUDIT_ID'] . '.');
		break;

	case 'LOOKUP_SUFFIX':
		$Viewer = new AssetViewer(AjaxGet('site_code', ''), AjaxGet('types', []));
		$Matches = $Viewer->FindBySuffix(AjaxRequire('suffix'));
		AjaxSuccess([
			'matches' => $Matches,
			'choices' => $Viewer->BuildLookupChoices($Matches)
		]);
		break;

	case 'IMPORT_BATCH':
		require_once(__DIR__ . '/../03-asset-batch-manager/asset.batch.manager.inc.php');
		$Manager = new AssetBatchManager($UserId);
		$Result = $Manager->GetValidatedAssetsByBatchCode(AjaxRequire('batch_code'));
		if (empty($Result['OK'])) {
			AjaxError($Result['ERROR_MESSAGE'], $Result['ERROR_CODE']);
		}
		AjaxSuccess([
			'batch_code' => $Result['BATCH_CODE'],
			'inputs' => array_column($Result['ASSETS'], 'ASSET_ID')
		], 'Loaded ' . count($Result['ASSETS']) . ' assets from batch ' . $Result['BATCH_CODE'] . '.');
		break;

	default:
		AjaxError('Invalid action.', 'INVALID_ACTION', 404);
}

?>
