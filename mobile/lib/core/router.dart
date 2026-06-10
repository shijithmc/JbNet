import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../features/auth/screens/login_screen.dart';
import '../features/auth/screens/register_screen.dart';
import '../features/jobs/screens/job_feed_screen.dart';
import '../features/jobs/screens/job_detail_screen.dart';
import '../features/referrals/screens/discover_paths_screen.dart';
import '../features/referrals/screens/referral_status_screen.dart';
import '../features/profile/screens/profile_screen.dart';
import '../features/connections/screens/connections_screen.dart';
import 'auth_state.dart';
import 'splash_screen.dart';

// ── FA-003: stable ChangeNotifier drives GoRouter.refreshListenable ──────────
//
// Previously `routerProvider` watched `authStateProvider` directly, causing a
// brand-new GoRouter to be constructed on every auth state change (destroying
// navigation history and causing screen flashes).
//
// The correct pattern: one stable GoRouter instance; auth changes are
// communicated via `refreshListenable` which tells the router to re-evaluate
// its `redirect` callback — without rebuilding the router.

class _AuthChangeNotifier extends ChangeNotifier {
  _AuthChangeNotifier(Ref ref) {
    ref.listen<AuthState>(authStateProvider, (_, _) => notifyListeners());
  }
}

/// Provides the [ChangeNotifier] that signals the router to re-run `redirect`.
/// The notifier itself is stable (never rebuilt); it only fires notifications.
final _authListenableProvider = Provider<_AuthChangeNotifier>((ref) {
  final notifier = _AuthChangeNotifier(ref);
  ref.onDispose(notifier.dispose);
  return notifier;
});

final routerProvider = Provider<GoRouter>((ref) {
  final listenable = ref.watch(_authListenableProvider);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: listenable, // triggers redirect re-eval without new GoRouter
    redirect: (context, state) {
      // Always read (not watch) — the listenable handles re-evaluation.
      final auth     = ref.read(authStateProvider);
      final location = state.matchedLocation;

      // FA-005: hold all redirects while session restore is in progress.
      if (auth.isRestoring) {
        return location == '/splash' ? null : '/splash';
      }

      final isLoggedIn  = auth.isAuthenticated;
      final isAuthRoute = location.startsWith('/auth');
      final isSplash    = location == '/splash';

      if (isSplash) {
        // Restore complete — route based on authentication result.
        return isLoggedIn ? '/jobs' : '/auth/login';
      }
      if (!isLoggedIn && !isAuthRoute) return '/auth/login';
      if (isLoggedIn && isAuthRoute)   return '/jobs';
      return null;
    },
    routes: [
      // Splash (shown during session restore)
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),

      // Auth
      GoRoute(path: '/auth/login',    builder: (_, _) => const LoginScreen()),
      GoRoute(path: '/auth/register', builder: (_, _) => const RegisterScreen()),

      // Main shell with bottom nav
      ShellRoute(
        builder: (context, state, child) => MainShell(child: child),
        routes: [
          GoRoute(
            path: '/jobs',
            builder: (_, _) => const JobFeedScreen(),
            routes: [
              GoRoute(
                path: ':id',
                builder: (_, state) =>
                    JobDetailScreen(jobId: state.pathParameters['id']!),
                routes: [
                  GoRoute(
                    path: 'paths',
                    builder: (_, state) => DiscoverPathsScreen(
                      jobId: state.pathParameters['id']!,
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: '/referrals/:id',
            builder: (_, state) =>
                ReferralStatusScreen(requestId: state.pathParameters['id']!),
          ),
          GoRoute(path: '/profile',     builder: (_, _) => const ProfileScreen()),
          GoRoute(path: '/connections', builder: (_, _) => const ConnectionsScreen()),
        ],
      ),
    ],
  );
});

class MainShell extends StatelessWidget {
  final Widget child;
  const MainShell({super.key, required this.child});

  @override
  Widget build(BuildContext context) {
    final location = GoRouterState.of(context).matchedLocation;

    int currentIndex = 0;
    if (location.startsWith('/jobs'))        currentIndex = 0;
    if (location.startsWith('/profile'))     currentIndex = 1;
    if (location.startsWith('/connections')) currentIndex = 2;

    return Scaffold(
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: currentIndex,
        onDestinationSelected: (i) {
          switch (i) {
            case 0: context.go('/jobs');        break;
            case 1: context.go('/profile');     break;
            case 2: context.go('/connections'); break;
          }
        },
        destinations: const [
          NavigationDestination(
              icon: Icon(Icons.work_outline),
              selectedIcon: Icon(Icons.work),
              label: 'Jobs'),
          NavigationDestination(
              icon: Icon(Icons.person_outline),
              selectedIcon: Icon(Icons.person),
              label: 'Profile'),
          NavigationDestination(
              icon: Icon(Icons.people_outline),
              selectedIcon: Icon(Icons.people),
              label: 'Network'),
        ],
      ),
    );
  }
}
