import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'core/router.dart';
import 'core/theme.dart';

void main() {
  // FA-010: Fail fast when required build-time constants are missing.
  // In debug mode this is an assertion; in release mode we show an error screen
  // instead of crashing silently with a cryptic Cognito error later.
  const poolId   = String.fromEnvironment('COGNITO_USER_POOL_ID');
  const clientId = String.fromEnvironment('COGNITO_CLIENT_ID');

  assert(
    poolId.isNotEmpty && clientId.isNotEmpty,
    'Build error: --dart-define=COGNITO_USER_POOL_ID and '
    '--dart-define=COGNITO_CLIENT_ID must be set.',
  );

  if (poolId.isEmpty || clientId.isEmpty) {
    // Release build with missing config — show a safe error screen.
    runApp(const _ConfigErrorApp());
    return;
  }

  runApp(const ProviderScope(child: JbNetApp()));
}

class JbNetApp extends ConsumerWidget {
  const JbNetApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);

    return MaterialApp.router(
      title: 'JbNet',
      theme: JbNetTheme.light,
      darkTheme: JbNetTheme.dark,
      themeMode: ThemeMode.system,
      routerConfig: router,
      debugShowCheckedModeBanner: false,
    );
  }
}

/// Shown in release builds when the app was built without the required
/// `--dart-define` configuration flags. FA-010.
class _ConfigErrorApp extends StatelessWidget {
  const _ConfigErrorApp();

  @override
  Widget build(BuildContext context) {
    return const MaterialApp(
      home: Scaffold(
        body: Center(
          child: Text(
            'App configuration error.\nPlease contact support.',
            textAlign: TextAlign.center,
          ),
        ),
      ),
    );
  }
}
