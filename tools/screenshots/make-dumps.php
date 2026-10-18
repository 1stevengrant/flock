<?php

// Writes the Dumps window's fixtures (dumps.json, dumps_header.html) with the dump server's own
// copy of symfony/var-dumper, so they look exactly like captured dumps:
//
//   php tools/screenshots/make-dumps.php

namespace App\Models {
    class User
    {
        public int $id = 42;
        public string $name = 'Ada Lovelace';
        public string $email = 'ada@acme.test';
        public array $roles = ['admin', 'billing'];
        public ?string $email_verified_at = '2026-03-14 09:26:53';
    }

    class Order
    {
        public int $id = 1042;
        public string $status = 'paid';
        public float $total = 129.9;
        public array $lines = [
            ['sku' => 'ACME-MUG', 'quantity' => 2, 'price' => 14.95],
            ['sku' => 'ACME-HOODIE', 'quantity' => 1, 'price' => 100.0],
        ];
    }
}

namespace {
    require __DIR__.'/../../internal/dumps/server/vendor/autoload.php';

    use Symfony\Component\VarDumper\Cloner\VarCloner;
    use Symfony\Component\VarDumper\Dumper\CliDumper;
    use Symfony\Component\VarDumper\Dumper\HtmlDumper;

    final class FixtureHtmlDumper extends HtmlDumper
    {
        public function header(): string
        {
            return $this->getDumpHeader();
        }
    }

    $fixtures = __DIR__.'/fixtures';
    file_put_contents("$fixtures/dumps_header.html", (new FixtureHtmlDumper())->header());

    $request = ['method' => 'GET', 'uri' => 'https://acme.test/orders/1042'];
    $dumps = [
        // Oldest first; the daemon lists them newest first (see below).
        ['ago' => '9m', 'value' => ['imported' => 128, 'skipped' => 3, 'took' => '1.84s'], 'label' => null,
            'source' => ['file' => 'C:\\Users\\demo\\Code\\acme\\app\\Console\\Commands\\ImportProducts.php', 'line' => 57, 'name' => 'ImportProducts.php'],
            'request' => null, 'command_line' => 'php artisan products:import', 'group' => 'cli-1'],
        ['ago' => '40s', 'value' => new App\Models\User(), 'label' => null,
            'source' => ['file' => 'C:\\Users\\demo\\Code\\acme\\app\\Http\\Controllers\\OrderController.php', 'line' => 31, 'name' => 'OrderController.php'],
            'request' => $request, 'command_line' => null, 'group' => 'request-2'],
        ['ago' => '40s', 'value' => new App\Models\Order(), 'label' => 'order',
            'source' => ['file' => 'C:\\Users\\demo\\Code\\acme\\app\\Http\\Controllers\\OrderController.php', 'line' => 32, 'name' => 'OrderController.php'],
            'request' => $request, 'command_line' => null, 'group' => 'request-2'],
        ['ago' => '40s', 'value' => 'select * from "orders" where "id" = ? limit 1', 'label' => null,
            'source' => ['file' => 'C:\\Users\\demo\\Code\\acme\\app\\Http\\Controllers\\OrderController.php', 'line' => 34, 'name' => 'OrderController.php'],
            'request' => $request, 'command_line' => null, 'group' => 'request-2'],
    ];

    $entries = [];
    foreach ($dumps as $index => $dump) {
        $data = (new VarCloner())->cloneVar($dump['value']);
        if ($dump['label'] !== null) {
            $data = $data->withContext(['label' => $dump['label']]);
        }
        $html = new HtmlDumper();
        $html->setDumpHeader('');
        $cli = new CliDumper();
        $cli->setColors(false);

        $entries[] = [
            'id' => sprintf('fixture-%d', $index + 1),
            'time' => '{{now-'.$dump['ago'].'}}',
            'label' => $dump['label'],
            'site' => 'acme',
            'source' => $dump['source'],
            'request' => $dump['request'],
            'command_line' => $dump['command_line'],
            'group' => $dump['group'],
            'text' => rtrim($cli->dump($data, true)),
            'html' => $html->dump($data, true),
        ];
    }

    file_put_contents("$fixtures/dumps.json", json_encode(array_reverse($entries), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE)."\n");
}
