<?php
$Projects = [
	[
		'PATH' => '01-configuration-analyzer/',
		'TITLE' => 'Configuration Analyzer',
		'DESCRIPTION' => 'Extract, filter, compare, and validate network access rules.'
	],
	[
		'PATH' => '02-asset-reconciliation/',
		'TITLE' => 'Asset Reconciliation',
		'DESCRIPTION' => 'Audit physical equipment against site inventory snapshots.'
	],
	[
		'PATH' => '03-asset-batch-manager/',
		'TITLE' => 'Asset Batch Manager',
		'DESCRIPTION' => 'Build validated equipment batches for downstream workflows.'
	],
	[
		'PATH' => '04-reusable-operations-toolkit/',
		'TITLE' => 'Operations Toolkit',
		'DESCRIPTION' => 'Shared AJAX, modal, search, styling, and reporting components.'
	]
];
?>
<!doctype html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title>Infrastructure Operations Suite</title>
	<link rel="stylesheet" href="04-reusable-operations-toolkit/simple.styles.css">
</head>
<body>
	<header class="app-header">
		<div class="app-mark">IO</div>
		<div>
			<h1>Infrastructure Operations Suite</h1>
			<div class="app-subtitle">Public business systems demonstration</div>
		</div>
	</header>
	<main class="page-shell">
		<section class="panel hero-panel">
			<div class="panel-title">Connected Business Systems</div>
			<div class="panel-content">
				<p>A modular PHP and JavaScript platform for network policy analysis, physical asset reconciliation, and controlled equipment workflows.</p>
				<div class="project-grid">
					<?php foreach ($Projects as $Project) { ?>
						<a class="project-card" href="<?php echo $Project['PATH']; ?>">
							<strong><?php echo $Project['TITLE']; ?></strong>
							<span><?php echo $Project['DESCRIPTION']; ?></span>
						</a>
					<?php } ?>
				</div>
			</div>
		</section>
	</main>
</body>
</html>
