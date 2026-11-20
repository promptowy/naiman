<?php
// Dedicated NAIMAN endpoint. The SQLite database lives outside the public directory.
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
    $db = new PDO('sqlite:/var/lib/naiman-public-stats/usage.sqlite', null, null,
        [PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION]);
    $db->exec('PRAGMA busy_timeout=2500');
    if ($method === 'POST' && $db->exec('UPDATE usage SET views=views+1 WHERE id=1') !== 1) {
        throw new RuntimeException('Counter not initialized');
    }
    $claim = $db->prepare('UPDATE usage SET retry=? WHERE id=1 AND retry<=?');
    $claim->execute([time()+90,time()]);
    if ($claim->rowCount() === 1) {
        try {
            $total = downloadsFromGithub();
            $db->prepare('UPDATE usage SET downloads=?,checked=?,retry=? WHERE id=1')->execute([$total,time(),time()+600]);
        } catch (Throwable $e) { error_log('NAIMAN downloads: ' . $e->getMessage()); }
    }
    $row = $db->query('SELECT views,started,downloads,checked FROM usage WHERE id=1')->fetch(PDO::FETCH_ASSOC);
    if (!$row) throw new RuntimeException('Counter not initialized');
    echo json_encode($row, JSON_THROW_ON_ERROR);
} catch (Throwable $e) {
    error_log('NAIMAN stats: ' . $e->getMessage());
    http_response_code(503);
    echo '{"error":"stats unavailable"}';
}
