import 'package:flutter_riverpod/flutter_riverpod.dart';

class AuthState {
  final bool isAuthenticated;
  final String? userId;
  final String? accessToken;

  const AuthState({
    this.isAuthenticated = false,
    this.userId,
    this.accessToken,
  });

  AuthState copyWith({bool? isAuthenticated, String? userId, String? accessToken}) =>
      AuthState(
        isAuthenticated: isAuthenticated ?? this.isAuthenticated,
        userId: userId ?? this.userId,
        accessToken: accessToken ?? this.accessToken,
      );
}

class AuthStateNotifier extends StateNotifier<AuthState> {
  AuthStateNotifier() : super(const AuthState());

  void setAuthenticated({required String userId, required String accessToken}) {
    state = state.copyWith(isAuthenticated: true, userId: userId, accessToken: accessToken);
  }

  void signOut() {
    state = const AuthState();
  }
}

final authStateProvider = StateNotifierProvider<AuthStateNotifier, AuthState>(
  (ref) => AuthStateNotifier(),
);
