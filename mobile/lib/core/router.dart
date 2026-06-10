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

final routerProvider = Provider<GoRouter>((ref) {
  final authState = ref.watch(authStateProvider);

  return GoRouter(
    initialLocation: '/jobs',
    redirect: (context, state) {
      final isLoggedIn = authState.isAuthenticated;
      final isAuthRoute = state.matchedLocation.startsWith('/auth');

      if (!isLoggedIn && !isAuthRoute) return '/auth/login';
      if (isLoggedIn && isAuthRoute) return '/jobs';
      return null;
    },
    routes: [
      // Auth
      GoRoute(path: '/auth/login',    builder: (ctx, _) => const LoginScreen()),
      GoRoute(path: '/auth/register', builder: (ctx, _) => const RegisterScreen()),

      // Main shell with bottom nav
      ShellRoute(
        builder: (context, state, child) => MainShell(child: child),
        routes: [
          GoRoute(
            path: '/jobs',
            builder: (ctx, _) => const JobFeedScreen(),
            routes: [
              GoRoute(
                path: ':id',
                builder: (ctx, state) => JobDetailScreen(jobId: state.pathParameters['id']!),
                routes: [
                  GoRoute(
                    path: 'paths',
                    builder: (ctx, state) => DiscoverPathsScreen(
                      jobId: state.pathParameters['id']!,
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(path: '/referrals/:id', builder: (ctx, state) =>
              ReferralStatusScreen(requestId: state.pathParameters['id']!)),
          GoRoute(path: '/profile',     builder: (ctx, _) => const ProfileScreen()),
          GoRoute(path: '/connections', builder: (ctx, _) => const ConnectionsScreen()),
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
          NavigationDestination(icon: Icon(Icons.work_outline), selectedIcon: Icon(Icons.work), label: 'Jobs'),
          NavigationDestination(icon: Icon(Icons.person_outline), selectedIcon: Icon(Icons.person), label: 'Profile'),
          NavigationDestination(icon: Icon(Icons.people_outline), selectedIcon: Icon(Icons.people), label: 'Network'),
        ],
      ),
    );
  }
}
