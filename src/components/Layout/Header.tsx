import useMovieGenres from '@/hooks/useMovieGenres';
import React from 'react';
import { Link } from 'react-router-dom';
import Search from '../Search/Search';
import AuthControls from '../Auth/AuthControls';
import logo from '@/assets/movieverse-logo.svg';

const Header: React.FC = () => {
  const { data: genres, isLoading: isLoadingGenres } = useMovieGenres();

  return (
    <header className="main-header">
      <div className="header-container flex justify-between gap-2 items-center h-20">
        <div className="flex items-center md:gap-8">
          <Link to="/" className="flex items-center shrink-0" aria-label="MovieVerse home">
            <img src={logo} alt="MovieVerse" width={280} height={64} className="h-10 sm:h-12 w-auto" />
          </Link>

          <nav className="hidden lg:block">
            {isLoadingGenres ? (
              <span className="text-gray-400">Loading Genres...</span>
            ) : (
              <ul className="flex gap-4 text-white">
                <li>
                  <Link to="/showtimes" className="font-semibold text-red-500 hover:text-red-400 transition">
                    Tickets
                  </Link>
                </li>
                {genres?.slice(0, 4).map((genre) => (
                  <li key={genre.id}>
                    <Link
                      to={`/genre/${genre.id}`}
                      className="hover:text-red-500 transition"
                    >
                      {genre.name}
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </nav>
        </div>

        <div className="flex items-center gap-2 sm:gap-4">
          <Search/>
          <AuthControls />
        </div>
      </div>
    </header>
  );
};

export default Header;
