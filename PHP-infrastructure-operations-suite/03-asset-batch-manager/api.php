<?php
require_once(__DIR__ . '/../04-reusable-operations-toolkit/simple.ajax.endpoint.inc.php');
require_once(__DIR__ . '/asset.batch.manager.inc.php');

$Request = GetAjaxAction();
$Manager = new AssetBatchManager(GetDemoUserId());

function ReturnBatchResult($Result, $Message)
{
	if (empty($Result['OK'])) {
		AjaxError($Result['ERROR_MESSAGE'] ?? 'Batch request failed.', $Result['ERROR_CODE'] ?? 'BATCH_ERROR');
	}
	AjaxSuccess($Result, $Message);
}

switch ($Request['ACTION']) {
	case 'GET_BATCH_CHOICES':
		AjaxSuccess(['choices' => $Manager->GetBatchChoices()]);
		break;

	case 'LOAD_BATCH':
		$Code = AjaxRequire('batch_code');
		ReturnBatchResult($Manager->LoadBatch($Code), 'Loaded batch ' . strtoupper($Code) . '.');
		break;

	case 'CREATE_BATCH':
		$Result = $Manager->CreateBatch(AjaxGet('batch_code', ''));
		ReturnBatchResult($Result, 'Created batch ' . ($Result['BATCH']['BATCH_CODE'] ?? '') . '.');
		break;

	case 'LOOKUP_SUFFIX':
		$Matches = DemoAssetRepository::FindBySuffix(AjaxRequire('suffix'));
		$Choices = array_map(function ($Asset) {
			return [
				'value' => $Asset['ASSET_ID'],
				'label' => $Asset['ASSET_ID'] . ' | ' . $Asset['SITE_CODE'] . ' | ' . $Asset['MODEL']
			];
		}, $Matches);
		AjaxSuccess(['matches' => $Matches, 'choices' => $Choices]);
		break;

	case 'ADD_ASSET':
		$Code = AjaxRequire('batch_code');
		ReturnBatchResult($Manager->AddAssetByScan($Code, AjaxRequire('input')), 'Added asset to batch ' . strtoupper($Code) . '.');
		break;

	case 'MASS_ADD':
		$Code = AjaxRequire('batch_code');
		$Inputs = AjaxGet('inputs', []);
		if (is_string($Inputs)) {
			$Inputs = preg_split('/\r?\n/', $Inputs);
		}
		$Result = $Manager->AddAssetsBatch($Code, $Inputs);
		ReturnBatchResult($Result, 'Added ' . ($Result['ADDED_COUNT'] ?? 0) . ' assets to batch ' . strtoupper($Code) . '.');
		break;

	case 'REMOVE_ASSET':
		$Code = AjaxRequire('batch_code');
		ReturnBatchResult($Manager->RemoveAsset($Code, AjaxRequire('asset_id')), 'Removed asset from batch ' . strtoupper($Code) . '.');
		break;

	case 'COMPLETE_BATCH':
		$Code = AjaxRequire('batch_code');
		ReturnBatchResult($Manager->CompleteBatch($Code, AjaxRequire('notes')), 'Completed batch ' . strtoupper($Code) . '.');
		break;

	case 'COPY_BATCH':
		$Code = AjaxRequire('batch_code');
		$Result = $Manager->CopyBatch($Code);
		ReturnBatchResult($Result, 'Copied batch ' . strtoupper($Code) . ' to ' . ($Result['BATCH']['BATCH_CODE'] ?? '') . '.');
		break;

	case 'GET_VALID_ASSETS':
		$Code = AjaxRequire('batch_code');
		ReturnBatchResult($Manager->GetValidatedAssetsByBatchCode($Code), 'Validated batch ' . strtoupper($Code) . '.');
		break;

	default:
		AjaxError('Invalid action.', 'INVALID_ACTION', 404);
}

?>
