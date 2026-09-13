<?php
/*
 * Asset Batch Manager
 * Groups validated assets into controlled workflow records.
 */

require_once(__DIR__ . '/../04-reusable-operations-toolkit/demo.asset.repository.inc.php');

define('ASSET_BATCH_CODE_CHARS', 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789');

class AssetBatchManager
{
	var $UserId;

	function __construct($UserId = 'demo.operator')
	{
		if (session_status() !== PHP_SESSION_ACTIVE) {
			session_start();
		}
		if (!isset($_SESSION['PORTFOLIO_ASSET_BATCHES'])) {
			$_SESSION['PORTFOLIO_ASSET_BATCHES'] = [];
			$_SESSION['PORTFOLIO_ASSET_BATCH_NEXT_INDEX'] = 0;
		}
		$this->UserId = trim((string)$UserId) ?: 'demo.operator';
	}

	#region Batch Lifecycle

	function CreateBatch($BatchCode = '')
	{
		$BatchCode = trim((string)$BatchCode) === '' ? $this->GetNewBatchCode() : $this->ValidateBatchCode($BatchCode);
		if ($BatchCode === '') {
			return $this->BuildError('INVALID_BATCH_CODE', 'Batch code must contain two supported letters or numbers.');
		}
		if ($this->BatchExists($BatchCode)) {
			return $this->BuildError('BATCH_EXISTS', 'Batch ' . $BatchCode . ' already exists.');
		}

		$Now = date('Y-m-d H:i:s');
		$_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode] = [
			'META' => [
				'BATCH_CODE' => $BatchCode,
				'COMPLETED' => 0,
				'CREATED_BY' => $this->UserId,
				'CREATED_DATE' => $Now,
				'UPDATED_BY' => $this->UserId,
				'UPDATED_DATE' => $Now,
				'COMPLETED_BY' => '',
				'COMPLETED_DATE' => '',
				'NOTES' => '',
				'COPIED_FROM' => ''
			],
			'ITEMS' => []
		];

		return $this->BuildSuccess(['BATCH' => $this->GetBatch($BatchCode), 'ITEMS' => []]);
	}

	function LoadBatch($BatchCode)
	{
		$BatchCode = $this->ValidateBatchCode($BatchCode);
		if (!$this->BatchExists($BatchCode)) {
			return $this->BuildError('BATCH_NOT_FOUND', 'Batch was not found.');
		}

		return $this->BuildSuccess([
			'BATCH' => $this->GetBatch($BatchCode),
			'ITEMS' => $this->GetBatchItems($BatchCode)
		]);
	}

	function CompleteBatch($BatchCode, $Notes)
	{
		$BatchCode = $this->ValidateBatchCode($BatchCode);
		$Notes = trim((string)$Notes);
		$Batch = $this->GetBatch($BatchCode);
		if (!$Batch) {
			return $this->BuildError('BATCH_NOT_FOUND', 'Batch was not found.');
		}
		if (!$this->UserCanEditBatch($Batch)) {
			return $this->BuildError('EDIT_DENIED', 'Only the batch owner can change this demo batch.');
		}
		if ($Batch['COMPLETED']) {
			return $this->BuildError('BATCH_COMPLETED', 'Batch is already completed.');
		}
		if (!$this->GetBatchItems($BatchCode)) {
			return $this->BuildError('BATCH_EMPTY', 'Add at least one asset before completing the batch.');
		}
		if ($Notes === '') {
			return $this->BuildError('NOTES_REQUIRED', 'Completion notes are required.');
		}

		$Meta =& $_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['META'];
		$Meta['COMPLETED'] = 1;
		$Meta['COMPLETED_BY'] = $this->UserId;
		$Meta['COMPLETED_DATE'] = date('Y-m-d H:i:s');
		$Meta['NOTES'] = $Notes;
		$this->TouchBatch($BatchCode);
		return $this->LoadBatch($BatchCode);
	}

	function CopyBatch($BatchCode)
	{
		$BatchCode = $this->ValidateBatchCode($BatchCode);
		$Batch = $this->GetBatch($BatchCode);
		if (!$Batch) {
			return $this->BuildError('BATCH_NOT_FOUND', 'Batch was not found.');
		}
		if (!$Batch['COMPLETED']) {
			return $this->BuildError('BATCH_OPEN', 'Only completed batches can be copied.');
		}

		$Created = $this->CreateBatch();
		if (empty($Created['OK'])) {
			return $Created;
		}
		$NewCode = $Created['BATCH']['BATCH_CODE'];
		$_SESSION['PORTFOLIO_ASSET_BATCHES'][$NewCode]['META']['COPIED_FROM'] = $BatchCode;
		foreach ($this->GetBatchItems($BatchCode) as $Asset) {
			$_SESSION['PORTFOLIO_ASSET_BATCHES'][$NewCode]['ITEMS'][$Asset['ASSET_ID']] = [
				'ASSET_ID' => $Asset['ASSET_ID'],
				'SERIAL' => $Asset['SERIAL'],
				'ADDED_BY' => $this->UserId,
				'ADDED_DATE' => date('Y-m-d H:i:s')
			];
		}
		$this->TouchBatch($NewCode);
		return $this->LoadBatch($NewCode);
	}

	#endregion

	#region Batch Items

	function AddAssetByScan($BatchCode, $Input)
	{
		$Asset = DemoAssetRepository::Find($Input);
		if (!$Asset) {
			return $this->BuildError('ASSET_NOT_FOUND', 'Asset was not found in the inventory source.');
		}

		return $this->AddAsset($BatchCode, $Asset);
	}

	function AddAssetsBatch($BatchCode, $Inputs)
	{
		$Inputs = array_values(array_unique(array_filter(array_map('trim', (array)$Inputs))));
		$Lookup = DemoAssetRepository::FindMany($Inputs);
		$Results = [];
		$Added = 0;
		foreach ($Inputs as $Input) {
			$Asset = $Lookup[DemoAssetRepository::Normalize($Input)] ?? [];
			$Result = $Asset ? $this->AddAsset($BatchCode, $Asset) : $this->BuildError('ASSET_NOT_FOUND', 'Asset was not found.');
			$Results[] = [
				'INPUT' => $Input,
				'OK' => !empty($Result['OK']),
				'ERROR_MESSAGE' => $Result['ERROR_MESSAGE'] ?? ''
			];
			if (!empty($Result['OK'])) {
				$Added++;
			}
		}

		$Loaded = $this->LoadBatch($BatchCode);
		$Loaded['RESULTS'] = $Results;
		$Loaded['ADDED_COUNT'] = $Added;
		return $Loaded;
	}

	function AddAsset($BatchCode, $Asset)
	{
		$BatchCode = $this->ValidateBatchCode($BatchCode);
		$Batch = $this->GetBatch($BatchCode);
		if (!$Batch) {
			return $this->BuildError('BATCH_NOT_FOUND', 'Batch was not found.');
		}
		if (!$this->UserCanEditBatch($Batch)) {
			return $this->BuildError('EDIT_DENIED', 'Only the batch owner can change this demo batch.');
		}
		if ($Batch['COMPLETED']) {
			return $this->BuildError('BATCH_COMPLETED', 'Completed batches cannot be changed.');
		}

		$AssetId = $Asset['ASSET_ID'];
		if (isset($_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['ITEMS'][$AssetId])) {
			return $this->BuildError('ASSET_EXISTS', 'Asset is already in batch ' . $BatchCode . '.');
		}

		$_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['ITEMS'][$AssetId] = [
			'ASSET_ID' => $AssetId,
			'SERIAL' => $Asset['SERIAL'],
			'ADDED_BY' => $this->UserId,
			'ADDED_DATE' => date('Y-m-d H:i:s')
		];
		$this->TouchBatch($BatchCode);
		return $this->LoadBatch($BatchCode);
	}

	function RemoveAsset($BatchCode, $AssetId)
	{
		$BatchCode = $this->ValidateBatchCode($BatchCode);
		$Batch = $this->GetBatch($BatchCode);
		if (!$Batch) {
			return $this->BuildError('BATCH_NOT_FOUND', 'Batch was not found.');
		}
		if (!$this->UserCanEditBatch($Batch) || $Batch['COMPLETED']) {
			return $this->BuildError('EDIT_DENIED', 'This batch cannot be changed.');
		}

		$AssetId = DemoAssetRepository::Normalize($AssetId);
		if (!isset($_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['ITEMS'][$AssetId])) {
			return $this->BuildError('ASSET_NOT_IN_BATCH', 'Asset is not in this batch.');
		}
		unset($_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['ITEMS'][$AssetId]);
		$this->TouchBatch($BatchCode);
		return $this->LoadBatch($BatchCode);
	}

	function GetValidatedAssetsByBatchCode($BatchCode)
	{
		$BatchCode = $this->ValidateBatchCode($BatchCode);
		$Batch = $this->GetBatch($BatchCode);
		if (!$Batch) {
			return $this->BuildError('BATCH_NOT_FOUND', 'Batch was not found.');
		}
		if (!$Batch['COMPLETED']) {
			return $this->BuildError('BATCH_OPEN', 'Complete the batch before using it in another workflow.');
		}

		$Assets = $this->GetBatchItems($BatchCode);
		if (!$Assets) {
			return $this->BuildError('BATCH_EMPTY', 'Batch does not contain any valid assets.');
		}

		return $this->BuildSuccess(['BATCH_CODE' => $BatchCode, 'ASSETS' => $Assets]);
	}

	#endregion

	#region Queries and Helpers

	function GetBatch($BatchCode)
	{
		$BatchCode = $this->ValidateBatchCode($BatchCode);
		if (!$this->BatchExists($BatchCode)) {
			return [];
		}

		$Meta = $_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['META'];
		$Meta['STATUS'] = $Meta['COMPLETED'] ? 'COMPLETED' : 'IN PROGRESS';
		$Meta['CAN_EDIT'] = $this->UserCanEditBatch($Meta) ? 1 : 0;
		$Meta['ITEM_COUNT'] = count($_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['ITEMS']);
		return $Meta;
	}

	function GetBatchItems($BatchCode)
	{
		$BatchCode = $this->ValidateBatchCode($BatchCode);
		if (!$this->BatchExists($BatchCode)) {
			return [];
		}

		$Rows = array_values($_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['ITEMS']);
		$Lookup = DemoAssetRepository::FindMany(array_column($Rows, 'ASSET_ID'));
		$Output = [];
		foreach ($Rows as $Row) {
			$Asset = $Lookup[DemoAssetRepository::Normalize($Row['ASSET_ID'])] ?? [];
			$Output[] = array_merge($Asset, $Row);
		}
		return $Output;
	}

	function GetBatchChoices()
	{
		$Choices = [];
		foreach (array_keys($_SESSION['PORTFOLIO_ASSET_BATCHES']) as $Code) {
			$Batch = $this->GetBatch($Code);
			$Choices[] = [
				'value' => $Code,
				'label' => $Code . ' | ' . $Batch['STATUS'] . ' | ' . $Batch['ITEM_COUNT'] . ' assets',
				'search' => $Code . ' ' . $Batch['STATUS'] . ' ' . $Batch['CREATED_BY']
			];
		}
		return array_reverse($Choices);
	}

	function GetNewBatchCode()
	{
		$Max = strlen(ASSET_BATCH_CODE_CHARS) ** 2;
		for ($Attempt = 0; $Attempt < $Max; $Attempt++) {
			$Index = (int)$_SESSION['PORTFOLIO_ASSET_BATCH_NEXT_INDEX']++ % $Max;
			$Code = $this->BuildCodeFromIndex($Index);
			if (!$this->BatchExists($Code)) {
				return $Code;
			}
		}
		return '';
	}

	function BuildCodeFromIndex($Index)
	{
		$Chars = ASSET_BATCH_CODE_CHARS;
		$Base = strlen($Chars);
		return $Chars[(int)floor($Index / $Base)] . $Chars[$Index % $Base];
	}

	function ValidateBatchCode($BatchCode)
	{
		$BatchCode = strtoupper(trim((string)$BatchCode));
		return preg_match('/^[' . ASSET_BATCH_CODE_CHARS . ']{2}$/', $BatchCode) ? $BatchCode : '';
	}

	function BatchExists($BatchCode)
	{
		return $BatchCode !== '' && isset($_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]);
	}

	function UserCanEditBatch($Batch)
	{
		return strtoupper((string)($Batch['CREATED_BY'] ?? '')) === strtoupper($this->UserId);
	}

	function TouchBatch($BatchCode)
	{
		$_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['META']['UPDATED_BY'] = $this->UserId;
		$_SESSION['PORTFOLIO_ASSET_BATCHES'][$BatchCode]['META']['UPDATED_DATE'] = date('Y-m-d H:i:s');
	}

	function BuildSuccess($Data = [])
	{
		$Data['OK'] = true;
		return $Data;
	}

	function BuildError($Code, $Message)
	{
		return ['OK' => false, 'ERROR_CODE' => $Code, 'ERROR_MESSAGE' => $Message];
	}

	#endregion
}

?>

