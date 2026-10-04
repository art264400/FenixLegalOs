# Fenix Legal OS (.NET 8)

Интеллектуальная юридическая диагностика технологических компаний и платёжная интеграция.
Продукт Fenix Law.

## Сборка и запуск

```bash
dotnet build
dotnet run
```

Запуск тестов:
```bash
dotnet test --filter "FullyQualifiedName~PaymentControllersTests"
```

## Просмотр и управление логами на сервере

Приложение выводит структурированные логи через стандартный `ILogger<T>` в stdout. 
На сервере под управлением systemd (`fenix.service`) вывод автоматически направляется в `systemd-journald`.

### Команды просмотра логов

```bash
# Логи в реальном времени
journalctl -u fenix.service -f

# Последние 200 строк
journalctl -u fenix.service -n 200

# Логи за последний час
journalctl -u fenix.service --since "1 hour ago"

# Используемое журналами место
journalctl --disk-usage
```

### Рекомендуемый лимит journald (необязательно)

Для ограничения занимаемого места журналами на диске можно создать конфигурационный файл:

```ini
# /etc/systemd/journald.conf.d/limits.conf
[Journal]
SystemMaxUse=300M
RuntimeMaxUse=100M
MaxRetentionSec=14day
```

Команда применения настроек:
```bash
systemctl restart systemd-journald
```
