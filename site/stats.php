<?php
// Install next to wp-load.php. Counters use atomic updates and do not store visitor identifiers.
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
header('Vary: Origin');
$sites = ['https://promptowy.com', 'https://promptowy.github.io'];
$origin = $_SERVER['HTTP_ORIGIN'] ?? '';
if (in_array($origin, $sites, true)) {
    header('Access-Control-Allow-Origin: ' . $origin);
}
$method = $_SERVER['REQUEST_METHOD'];
if (!in_array($method, ['GET', 'POST'], true)) {
    header('Allow: GET, POST');
    http_response_code(405);
    exit;
}
if ($method === 'POST' && (!in_array($origin, $sites, true) ||
    file_get_contents('php://input', false, null, 0, 16) !== 'pageview')) {
    http_response_code(403);
    exit;
}

function downloadsFromGithub(): int {
    $total = 0;
    $found = false;
    for ($page = 1; $page <= 30; $page++) {
        $curl = curl_init('https://api.github.com/repos/promptowy/naiman/releases?per_page=100&page=' . $page);
        curl_setopt_array($curl, [CURLOPT_RETURNTRANSFER => true, CURLOPT_CONNECTTIMEOUT => 3,
            CURLOPT_TIMEOUT => 6, CURLOPT_USERAGENT => 'NAIMAN-landing-stats']);
        $body = curl_exec($curl);
        $status = curl_getinfo($curl, CURLINFO_RESPONSE_CODE);
        curl_close($curl);
        $items = is_string($body) ? json_decode($body, true) : null;
        if ($status !== 200 || !is_array($items)) throw new RuntimeException('GitHub unavailable');
        foreach ($items as $release) {
            if (!isset($release['assets']) || !is_array($release['assets'])) throw new RuntimeException('Invalid release');
            foreach ($release['assets'] as $asset) {
                if ($asset['name'] === 'NAIMAN.exe' && $asset['state'] === 'uploaded') {
                    if (!is_int($asset['download_count']) || $asset['download_count'] < 0) throw new RuntimeException('Invalid count');
                    $total += $asset['download_count'];
                    $found = true;
                }
            }
        }
        if (count($items) < 100) {
            if (!$found) throw new RuntimeException('No release assets');
            return $total;
        }
    }
    throw new RuntimeException('Incomplete release list');
}

try {
    define('SHORTINIT', true);
    require __DIR__ . '/wp-load.php';
    $table = $wpdb->options;
    $delta = $method === 'POST' ? 1 : 0;
    $write = $wpdb->query($wpdb->prepare("INSERT INTO $table (option_name,option_value,autoload) VALUES
        ('naiman_stats_views',%d,'no'),('naiman_stats_started',%d,'no'),('naiman_stats_retry',0,'no')
        ON DUPLICATE KEY UPDATE option_value=IF(option_name='naiman_stats_views',option_value+%d,option_value)",
        $delta, time(), $delta));
    if ($write === false) throw new RuntimeException('counter write failed');
    // Only one request refreshes the shared cache. Errors retain the last successful count.
    $claim = $wpdb->query($wpdb->prepare("UPDATE $table SET option_value=%d WHERE option_name='naiman_stats_retry'
        AND CAST(option_value AS UNSIGNED)<=%d", time()+90, time()));
    if ($claim === 1) {
        try {
            $count = downloadsFromGithub();
            $snapshot = json_encode(['downloads'=>$count, 'checked'=>time()]);
            $wpdb->query($wpdb->prepare("INSERT INTO $table (option_name,option_value,autoload) VALUES
                ('naiman_stats_snapshot',%s,'no'),('naiman_stats_retry',%d,'no')
                ON DUPLICATE KEY UPDATE option_value=VALUES(option_value)", $snapshot, time()+600));
        } catch (RuntimeException $e) { error_log('NAIMAN download counter: ' . $e->getMessage()); }
    }
    $rows = $wpdb->get_results("SELECT option_name,option_value FROM $table WHERE option_name IN
        ('naiman_stats_views','naiman_stats_started','naiman_stats_snapshot')", ARRAY_A);
    $data = array_column($rows, 'option_value', 'option_name');
    if (!isset($data['naiman_stats_views'], $data['naiman_stats_started'])) throw new RuntimeException('counter read failed');
    $snapshot = json_decode($data['naiman_stats_snapshot'] ?? '', true);
    echo json_encode(['views'=>(int)$data['naiman_stats_views'], 'started'=>(int)$data['naiman_stats_started'],
        'downloads'=>$snapshot['downloads'] ?? null, 'checked'=>$snapshot['checked'] ?? null], JSON_THROW_ON_ERROR);
} catch (Throwable $e) {
    error_log('NAIMAN stats: ' . $e->getMessage());
    http_response_code(503);
    echo '{"error":"stats unavailable"}';
}
