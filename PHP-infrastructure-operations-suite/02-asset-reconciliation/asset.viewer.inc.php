<?php
/* Inventory lookup adapter for reconciliation workflows. */

require_once(__DIR__ . '/../04-reusable-operations-toolkit/demo.asset.repository.inc.php');

class AssetViewer
{
	var $SiteCode;
	var $Types;

	function __construct($SiteCode = '', $Types = [])
	{
		$this->SetSite($SiteCode);
		$this->SetTypes($Types);
	}

	function SetSite($SiteCode)
	{
		$this->SiteCode = strtoupper(trim((string)$SiteCode));
	}

	function SetTypes($Types)
	{
		$this->Types = DemoAssetRepository::NormalizeTypes($Types);
	}

	function GetSite() { return $this->SiteCode; }
	function GetTypes() { return $this->Types; }
	function GetTypesString() { return implode(',', $this->Types); }

	function GetExpectedAssets()
	{
		return DemoAssetRepository::GetBySite($this->SiteCode, $this->Types);
	}

	function FindAsset($Value)
	{
		return DemoAssetRepository::Find($Value, $this->Types);
	}

	function FindAssets($Values)
	{
		return DemoAssetRepository::FindMany($Values, $this->Types);
	}

	function FindBySuffix($Suffix)
	{
		return DemoAssetRepository::FindBySuffix($Suffix, $this->Types);
	}

	function BuildLookupChoices($Assets)
	{
		$Choices = [];
		foreach ($Assets as $Asset) {
			$Choices[] = [
				'value' => $Asset['ASSET_ID'],
				'label' => $Asset['ASSET_ID'] . ' | ' . $Asset['SITE_CODE'] . ' | ' . $Asset['MODEL']
			];
		}

		return $Choices;
	}
}

?>

