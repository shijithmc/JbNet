import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:jbnet/core/auth_state.dart';
import 'package:jbnet/main.dart';

void main() {
  testWidgets('App renders without crashing', (WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          // Start in unauthenticated state — should render the login screen
          authStateProvider.overrideWith((ref) => AuthStateNotifier()),
        ],
        child: const JbNetApp(),
      ),
    );
    await tester.pumpAndSettle();

    // Login screen shows "JbNet" title and sign-in button
    expect(find.text('JbNet'), findsOneWidget);
    expect(find.text('Sign in'), findsOneWidget);
  });
}
