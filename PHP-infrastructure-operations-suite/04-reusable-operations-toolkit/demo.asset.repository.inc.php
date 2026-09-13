<?php
/*
 * Demo Asset Repository
 * Replace this adapter with organization inventory storage.
 */

class DemoAssetRepository
{
	static function GetAll()
	{
		return [
			['ASSET_ID' => '02A000000101', 'SERIAL' => 'DEMO-0001', 'MODEL' => 'Desk Endpoint A', 'SITE_CODE' => 'NORTH-01', 'TYPE' => 'ENDPOINT', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02A000000102', 'SERIAL' => 'DEMO-0002', 'MODEL' => 'Desk Endpoint A', 'SITE_CODE' => 'NORTH-01', 'TYPE' => 'ENDPOINT', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02A000000103', 'SERIAL' => 'DEMO-0003', 'MODEL' => 'Conference Endpoint', 'SITE_CODE' => 'NORTH-01', 'TYPE' => 'ENDPOINT', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02B000000111', 'SERIAL' => 'DEMO-0011', 'MODEL' => 'Branch Router X', 'SITE_CODE' => 'NORTH-01', 'TYPE' => 'NETWORK_APPLIANCE', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02B000000112', 'SERIAL' => 'DEMO-0012', 'MODEL' => 'Access Switch 24', 'SITE_CODE' => 'NORTH-01', 'TYPE' => 'NETWORK_APPLIANCE', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02A000000201', 'SERIAL' => 'DEMO-0021', 'MODEL' => 'Desk Endpoint B', 'SITE_CODE' => 'EAST-02', 'TYPE' => 'ENDPOINT', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02A000000202', 'SERIAL' => 'DEMO-0022', 'MODEL' => 'Desk Endpoint B', 'SITE_CODE' => 'EAST-02', 'TYPE' => 'ENDPOINT', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02A000000203', 'SERIAL' => 'DEMO-0023', 'MODEL' => 'Conference Endpoint', 'SITE_CODE' => 'EAST-02', 'TYPE' => 'ENDPOINT', 'PENDING_FLAG' => 'Y'],
			['ASSET_ID' => '02B000000211', 'SERIAL' => 'DEMO-0031', 'MODEL' => 'Branch Router Y', 'SITE_CODE' => 'EAST-02', 'TYPE' => 'NETWORK_APPLIANCE', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02B000000212', 'SERIAL' => 'DEMO-0032', 'MODEL' => 'Access Switch 48', 'SITE_CODE' => 'EAST-02', 'TYPE' => 'NETWORK_APPLIANCE', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02C000000301', 'SERIAL' => 'DEMO-0041', 'MODEL' => 'Lab Sensor One', 'SITE_CODE' => 'LAB-03', 'TYPE' => 'TEST_DEVICE', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02C000000302', 'SERIAL' => 'DEMO-0042', 'MODEL' => 'Lab Sensor Two', 'SITE_CODE' => 'LAB-03', 'TYPE' => 'TEST_DEVICE', 'PENDING_FLAG' => 'N'],
			['ASSET_ID' => '02B000000311', 'SERIAL' => 'DEMO-0051', 'MODEL' => 'Lab Gateway', 'SITE_CODE' => 'LAB-03', 'TYPE' => 'NETWORK_APPLIANCE', 'PENDING_FLAG' => 'N']
		];
	}

	static function GetSites()
	{
		return [
			['VALUE' => 'NORTH-01', 'LABEL' => 'NORTH-01 - North Operations'],
			['VALUE' => 'EAST-02', 'LABEL' => 'EAST-02 - East Distribution'],
			['VALUE' => 'LAB-03', 'LABEL' => 'LAB-03 - Product Lab']
		];
	}

	static function GetTypes()
	{
		return ['ENDPOINT', 'NETWORK_APPLIANCE', 'TEST_DEVICE'];
	}

	static function Normalize($Value)
	{
		return strtoupper(preg_replace('/[^A-Z0-9]/i', '', (string)$Value));
	}

	static function NormalizeTypes($Types)
	{
		if (is_string($Types)) {
			$Types = explode(',', $Types);
		}
		if (!is_array($Types)) {
			$Types = [];
		}

		$Allowed = self::GetTypes();
		$Output = [];
		foreach ($Types as $Type) {
			$Type = strtoupper(trim((string)$Type));
			if (in_array($Type, $Allowed, true) && !in_array($Type, $Output, true)) {
				$Output[] = $Type;
			}
		}

		sort($Output);
		return $Output ?: $Allowed;
	}

	static function GetBySite($SiteCode, $Types = [])
	{
		$SiteCode = strtoupper(trim((string)$SiteCode));
		$Types = self::NormalizeTypes($Types);
		return array_values(array_filter(self::GetAll(), function ($Asset) use ($SiteCode, $Types) {
			return $Asset['SITE_CODE'] === $SiteCode && in_array($Asset['TYPE'], $Types, true);
		}));
	}

	static function Find($Value, $Types = [])
	{
		$Needle = self::Normalize($Value);
		$Types = self::NormalizeTypes($Types);
		foreach (self::GetAll() as $Asset) {
			if (!in_array($Asset['TYPE'], $Types, true)) {
				continue;
			}
			if (self::Normalize($Asset['ASSET_ID']) === $Needle || self::Normalize($Asset['SERIAL']) === $Needle) {
				return $Asset;
			}
		}

		return [];
	}

	static function FindMany($Values, $Types = [])
	{
		$Types = self::NormalizeTypes($Types);
		$Needles = [];
		foreach ((array)$Values as $Value) {
			$Key = self::Normalize($Value);
			if ($Key !== '') {
				$Needles[$Key] = true;
			}
		}

		$Output = [];
		foreach (self::GetAll() as $Asset) {
			if (!in_array($Asset['TYPE'], $Types, true)) {
				continue;
			}
			$AssetKey = self::Normalize($Asset['ASSET_ID']);
			$SerialKey = self::Normalize($Asset['SERIAL']);
			if (isset($Needles[$AssetKey]) || isset($Needles[$SerialKey])) {
				$Output[$AssetKey] = $Asset;
				$Output[$SerialKey] = $Asset;
			}
		}

		return $Output;
	}

	static function FindBySuffix($Suffix, $Types = [])
	{
		$Suffix = self::Normalize($Suffix);
		if (strlen($Suffix) < 4 || strlen($Suffix) > 7) {
			return [];
		}

		$Types = self::NormalizeTypes($Types);
		return array_values(array_filter(self::GetAll(), function ($Asset) use ($Suffix, $Types) {
			return in_array($Asset['TYPE'], $Types, true)
				&& substr(self::Normalize($Asset['ASSET_ID']), -strlen($Suffix)) === $Suffix;
		}));
	}
}

?>
